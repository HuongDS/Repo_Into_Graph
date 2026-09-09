# -*- coding: utf-8 -*-
"""
TANG 2 - HYBRID CONTEXT GENERATOR (Python, cung tien trinh voi Tang 1)

Toan bo Tang 2 chay in-memory tren cay cu phap cua tree-sitter:
    ma nguon -> cay cu phap (tree-sitter) -> cay cau lenh -> CFG
             -> Mermaid + Critical Snippets + Enriched Metadata -> hybrid_prompt

Khong co buoc tuan tu hoa trung gian, khong goi HTTP vong lai, va chi co
MOT bo phan tich duy nhat cho ca Java lan C# - nen chat luong prompt dua vao
LLM luon nhat quan giua cac lan chay (yeu cau bat buoc cua do luong khoa hoc).

.NET chi dong vai tro dieu phoi: goi mot lan /api/generate-hybrid-context roi
tra nguyen ket qua ve cho Orchestrator.
"""
import re
import time
from datetime import datetime, timezone

from ast_analyzer import analyze_ast
from cfg_structure import parse_structure, mask_source

MAX_NODES = 600
MAX_LABEL = 70
MAX_SNIPPETS = 40
MAX_SNIPPET_LENGTH = 400
PROMPT_WEIGHT_THRESHOLD = 7
MAX_SOURCE_LENGTH = 200_000


# =====================================================================
# 1. NHAN NODE
# =====================================================================
_CALL_RE = re.compile(
    r'^(?P<await>await\s+)?(?P<recv>[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*?\s*\.\s*)?'
    r'(?P<name>[A-Za-z_]\w*)\s*\((?P<args>.*)\)$', re.S)
_ASSIGN_RE = re.compile(r'^(?P<lhs>[A-Za-z_][\w.\[\]<>, ]*?)\s*(?P<op>=|\+=|-=|\*=|/=|%=|\|=|&=)\s*(?P<rhs>.+)$', re.S)
_DECL_RE = re.compile(r'^(?:final\s+|const\s+|readonly\s+)?(?P<type>[A-Za-z_][\w.<>,\[\]\s]*?)\s+'
                      r'(?P<name>[A-Za-z_]\w*)\s*=\s*(?P<rhs>.+)$', re.S)
_STEP_RE = re.compile(r'^(?P<n>[A-Za-z_][\w.\[\]]*)\s*(?P<op>\+\+|--)$|^(?P<op2>\+\+|--)\s*(?P<n2>[A-Za-z_][\w.\[\]]*)$')
_SIMPLE_CALL_COND_RE = re.compile(r'^(?P<neg>!?\s*)(?P<name>[A-Za-z_][\w.]*)\s*\([^()]*\)$')
_CTRL_WORDS = {"if", "while", "for", "foreach", "switch", "catch", "return", "throw",
               "using", "lock", "synchronized"}


def collapse(text):
    return re.sub(r"\s+", " ", text or "").strip()


def truncate(text, limit=MAX_LABEL):
    value = collapse(text)
    return value if len(value) <= limit else value[:max(1, limit - 3)] + "..."


def _question(label):
    if not label:
        return "condition?"
    return label if label.endswith("?") else label + "?"


def humanize(identifier):
    """sendAsync -> Send Async ; save -> Save"""
    out = ""
    for i, ch in enumerate(identifier or ""):
        if ch == "_":
            out += " "
            continue
        if i > 0 and ch.isupper() and not identifier[i - 1].isupper():
            out += " "
        out += ch
    out = collapse(out)
    return out[:1].upper() + out[1:] if out else out


def short_exception(name):
    """OverloadException -> Overload"""
    value = (name or "").split(".")[-1]
    for suffix in ("Exception", "Error"):
        if len(value) > len(suffix) and value.endswith(suffix):
            return value[:-len(suffix)]
    return value


def label_decision(condition):
    """req == null -> 'req == null?' ; isConflict(req) -> 'isConflict?'"""
    value = collapse(condition)
    if not value:
        return "condition?"
    m = _SIMPLE_CALL_COND_RE.match(value)
    if m:
        value = m.group("neg").replace(" ", "") + m.group("name")
    return _question(truncate(value))


def label_loop(kind, stmt):
    condition = collapse(stmt.get("condition"))
    if kind == "ForEach":
        m = re.match(r"^(?P<decl>.+?)\s*(?::|\bin\b)\s*(?P<src>.+)$", condition)
        if m:
            ids = re.findall(r"[A-Za-z_]\w*", m.group("decl"))
            name = ids[-1] if ids else m.group("decl")
            return _question(truncate("For each " + name + " in " + m.group("src")))
        return _question(truncate("For each " + condition))
    if kind == "DoWhile":
        return _question(truncate("Repeat while " + (condition or "true")))
    return _question(truncate("Loop while " + (condition or "true")))


def label_switch(selector):
    value = collapse(selector)
    return _question(truncate("Switch on " + value)) if value else "Switch?"


def label_catch(exception_type):
    return truncate("Catch " + short_exception(exception_type or "Exception"))


def label_return(text):
    value = collapse(text).rstrip(";").strip()
    if value.startswith("return"):
        value = value[6:].strip()
    return truncate("Return " + value) if value else "Return"


def thrown_type(text):
    m = re.search(r"throw\s+new\s+([A-Za-z_][\w.]*)", text or "")
    return m.group(1).split(".")[-1] if m else ""


def label_throw(text):
    t = thrown_type(text)
    if t:
        return truncate("Throw " + short_exception(t))
    value = collapse(text).rstrip(";").strip()
    if value.startswith("throw"):
        value = value[5:].strip()
    return truncate("Throw " + value) if value else "Throw"


def _simple_arguments(args):
    value = collapse(args)
    if not value:
        return ""
    parts = []
    for raw in value.split(","):
        part = raw.strip()
        if not part:
            continue
        if not re.match(r"^[A-Za-z_]\w*$", part) and not re.match(r"^-?\d+(\.\d+)?$", part):
            return ""
        parts.append(part)
    return "" if not parts or len(parts) > 3 else " ".join(parts)


def label_process(text):
    """save(req) -> 'Save req' ; double total = 0 -> 'Set total = 0'"""
    value = collapse(text).rstrip(";").strip()
    if not value:
        return "(empty)"

    m = _CALL_RE.match(value)
    if m and m.group("name") not in _CTRL_WORDS:
        label = humanize(m.group("name"))
        args = _simple_arguments(m.group("args"))
        if args:
            label += " " + args
        receiver = (m.group("recv") or "").strip().rstrip(".").strip()
        if receiver and receiver not in ("this", "base", "super"):
            label += " (" + receiver + ")"
        if m.group("await"):
            label = "Await " + label
        return truncate(label)

    m = _DECL_RE.match(value)
    if m and not value.startswith("return"):
        return truncate("Set " + m.group("name") + " = " + m.group("rhs"))

    m = _ASSIGN_RE.match(value)
    if m:
        op = m.group("op")
        return truncate(("Set " if op == "=" else "Update ") + collapse(m.group("lhs")) + " " + op + " " + m.group("rhs"))

    m = _STEP_RE.match(value)
    if m:
        name = m.group("n") or m.group("n2")
        op = m.group("op") or m.group("op2")
        return truncate(("Increase " if op == "++" else "Decrease ") + name)

    return truncate(value)


# =====================================================================
# 2. DUNG CONTROL FLOW GRAPH
# =====================================================================
def alpha_id(index):
    """0 -> A, 25 -> Z, 26 -> AA"""
    out = ""
    value = index + 1
    while value > 0:
        value -= 1
        out = chr(ord("A") + value % 26) + out
        value //= 26
    return out


class CfgBuilder:
    def __init__(self):
        self.nodes = []
        self.edges = []
        self._edge_keys = set()
        self._seq = 0
        self._depth = 0
        self._truncated_id = None
        self._method = ""
        self._returns = []
        self._abnormal = []
        self._breaks = []
        self._loops = []
        self._tries = []
        self.stmt_node_map = {}
        self.stats = {"branchCount": 0, "loopCount": 0, "switchCount": 0, "tryCatchCount": 0,
                      "throwCount": 0, "returnCount": 0, "maxNestingDepth": 0, "hasComplexLoop": False}

    @property
    def truncated(self):
        return self._truncated_id is not None

    # ── node / edge ──────────────────────────────────────────────
    def add_node(self, label, kind, line=0, code=""):
        if len(self.nodes) >= MAX_NODES:
            if self._truncated_id is None:
                self._truncated_id = alpha_id(self._seq)
                self._seq += 1
                self.nodes.append({"id": self._truncated_id, "label": "... CFG rut gon ...",
                                   "kind": "PROCESS", "line": 0, "code": "", "method": self._method})
            return self._truncated_id

        node_id = alpha_id(self._seq)
        self._seq += 1
        self.nodes.append({"id": node_id, "label": label or "(empty)", "kind": kind,
                           "line": line, "code": collapse(code), "method": self._method})
        return node_id

    def add_edge(self, src, dst, label=""):
        if not src or not dst:
            return
        if src == dst and not label:
            return
        key = "%s->%s#%s" % (src, dst, label)
        if key in self._edge_keys:
            return
        self._edge_keys.add(key)
        self.edges.append({"from": src, "to": dst, "label": label or ""})

    def connect(self, stubs, target, override=""):
        for src, label in stubs:
            self.add_edge(src, target, label or override)

    def _enter(self):
        self._depth += 1
        self.stats["maxNestingDepth"] = max(self.stats["maxNestingDepth"], self._depth)

    def _exit(self):
        self._depth = max(0, self._depth - 1)

    # ── ham ──────────────────────────────────────────────────────
    def build_method(self, method):
        self._method = method.get("name") or "main"
        self._returns, self._abnormal = [], []
        self._breaks, self._loops, self._tries = [], [], []

        start = self.add_node("Start", "START", method.get("startLine", 0), method.get("signature", ""))
        outs = self.build_block(method.get("body", []), [(start, "")])

        end = self.add_node("End", "END", 0, "")
        self.connect(outs, end)
        self.connect(self._returns, end)
        for src, label in self._abnormal:
            self.add_edge(src, end, label or "throws")

    def build_block(self, statements, incoming):
        current = incoming
        for stmt in statements or []:
            current = self.build_statement(stmt, current)
        return current

    def build_statement(self, stmt, incoming):
        before = len(self.nodes)
        result = self._build(stmt, incoming)
        if len(self.nodes) > before and stmt.get("start") not in self.stmt_node_map:
            self.stmt_node_map[stmt.get("start")] = self.nodes[before]["id"]
        return result

    def _build(self, stmt, incoming):
        kind = stmt.get("kind")

        if kind == "Block":
            return self.build_block(stmt.get("body"), incoming)

        if kind == "If":
            self.stats["branchCount"] += 1
            decision = self.add_node(label_decision(stmt.get("condition")), "DECISION",
                                     stmt.get("line", 0), stmt.get("headerText", ""))
            self.connect(incoming, decision)
            self._enter()
            then_outs = self.build_block(stmt.get("body"), [(decision, "Yes")])
            else_outs = (self.build_block(stmt.get("else"), [(decision, "No")])
                         if stmt.get("else") else [(decision, "No")])
            self._exit()
            return then_outs + else_outs

        if kind in ("While", "ForEach"):
            self.stats["loopCount"] += 1
            decision = self.add_node(label_loop(kind, stmt), "DECISION",
                                     stmt.get("line", 0), stmt.get("headerText", ""))
            self.connect(incoming, decision)
            breaks = []
            self._breaks.append(breaks)
            self._loops.append(decision)
            self._enter()
            body_outs = self.build_block(stmt.get("body"), [(decision, "Yes")])
            self.connect(body_outs, decision, "loop")
            self._exit()
            self._loops.pop()
            self._breaks.pop()
            return [(decision, "No")] + breaks

        if kind == "For":
            self.stats["loopCount"] += 1
            entry = incoming
            if collapse(stmt.get("forInit")):
                init_id = self.add_node("Init " + truncate(stmt.get("forInit")), "PROCESS",
                                        stmt.get("line", 0), stmt.get("forInit"))
                self.connect(entry, init_id)
                entry = [(init_id, "")]

            decision = self.add_node(label_loop("For", stmt), "DECISION",
                                     stmt.get("line", 0), stmt.get("headerText", ""))
            self.connect(entry, decision)

            update_id = None
            if collapse(stmt.get("forUpdate")):
                update_id = self.add_node("Next " + truncate(stmt.get("forUpdate")), "PROCESS",
                                          stmt.get("line", 0), stmt.get("forUpdate"))

            breaks = []
            self._breaks.append(breaks)
            self._loops.append(update_id or decision)
            self._enter()
            body_outs = self.build_block(stmt.get("body"), [(decision, "Yes")])
            if update_id:
                self.connect(body_outs, update_id)
                self.add_edge(update_id, decision, "loop")
            else:
                self.connect(body_outs, decision, "loop")
            self._exit()
            self._loops.pop()
            self._breaks.pop()
            return [(decision, "No")] + breaks

        if kind == "DoWhile":
            self.stats["loopCount"] += 1
            entry = self.add_node("Do", "PROCESS", stmt.get("line", 0), "do")
            self.connect(incoming, entry)
            decision = self.add_node(label_loop("DoWhile", stmt), "DECISION",
                                     stmt.get("line", 0), stmt.get("headerText", ""))
            breaks = []
            self._breaks.append(breaks)
            self._loops.append(decision)
            self._enter()
            body_outs = self.build_block(stmt.get("body"), [(entry, "")])
            self.connect(body_outs, decision)
            self._exit()
            self._loops.pop()
            self._breaks.pop()
            self.add_edge(decision, entry, "Yes")
            return [(decision, "No")] + breaks

        if kind == "Switch":
            self.stats["switchCount"] += 1
            decision = self.add_node(label_switch(stmt.get("condition")), "DECISION",
                                     stmt.get("line", 0), stmt.get("headerText", ""))
            self.connect(incoming, decision)
            breaks = []
            self._breaks.append(breaks)
            self._enter()
            outs, has_default = [], False
            for branch in stmt.get("cases", []):
                if branch.get("isDefault"):
                    has_default = True
                edge_label = "default" if branch.get("isDefault") else "case " + truncate(branch.get("label"), 24)
                outs += self.build_block(branch.get("body"), [(decision, edge_label)])
            self._exit()
            self._breaks.pop()
            if not has_default:
                outs.append((decision, "default"))
            return outs + breaks

        if kind == "Try":
            self.stats["tryCatchCount"] += 1
            code = "try" if not collapse(stmt.get("condition")) else "try (" + collapse(stmt.get("condition")) + ")"
            try_id = self.add_node("Try", "PROCESS", stmt.get("line", 0), code)
            self.connect(incoming, try_id)

            catch_ids = []
            for clause in stmt.get("catches", []):
                catch_id = self.add_node(label_catch(clause.get("exceptionType")), "CATCH",
                                         clause.get("line", 0), clause.get("headerText", ""))
                if clause.get("start"):
                    self.stmt_node_map[clause["start"]] = catch_id
                catch_ids.append((catch_id, clause.get("exceptionType") or ""))

            self._tries.append(catch_ids)
            self._enter()
            body_outs = self.build_block(stmt.get("body"), [(try_id, "")])
            self._exit()
            self._tries.pop()

            for catch_id, _ in catch_ids:
                self.add_edge(try_id, catch_id, "exception")

            all_outs = list(body_outs)
            for i, clause in enumerate(stmt.get("catches", [])):
                if i < len(catch_ids):
                    self._enter()
                    all_outs += self.build_block(clause.get("body"), [(catch_ids[i][0], "")])
                    self._exit()

            if stmt.get("finally"):
                finally_id = self.add_node("Finally", "FINALLY", stmt.get("line", 0), "finally")
                self.connect(all_outs, finally_id)
                self._enter()
                all_outs = self.build_block(stmt.get("finally"), [(finally_id, "")])
                self._exit()

            return all_outs

        if kind == "Scoped":
            node_id = self.add_node(truncate(stmt.get("headerText")), "PROCESS",
                                    stmt.get("line", 0), stmt.get("headerText", ""))
            self.connect(incoming, node_id)
            self._enter()
            outs = self.build_block(stmt.get("body"), [(node_id, "")])
            self._exit()
            return outs

        if kind == "Return":
            self.stats["returnCount"] += 1
            node_id = self.add_node(label_return(stmt.get("text")), "RETURN",
                                    stmt.get("line", 0), stmt.get("text", ""))
            self.connect(incoming, node_id)
            self._returns.append((node_id, ""))
            return []

        if kind == "Throw":
            self.stats["throwCount"] += 1
            node_id = self.add_node(label_throw(stmt.get("text")), "THROW",
                                    stmt.get("line", 0), stmt.get("text", ""))
            self.connect(incoming, node_id)
            self._register_throw(node_id, stmt.get("text", ""))
            return []

        if kind == "Break":
            if self._breaks:
                self._breaks[-1].extend(incoming)
            return []

        if kind == "Continue":
            if self._loops:
                target = self._loops[-1]
                for src, label in incoming:
                    self.add_edge(src, target, label or "continue")
            return []

        node_id = self.add_node(label_process(stmt.get("text")), "PROCESS",
                                stmt.get("line", 0), stmt.get("text", ""))
        self.connect(incoming, node_id)
        return [(node_id, "")]

    def _register_throw(self, node_id, text):
        if self._tries and self._tries[-1]:
            wanted = thrown_type(text).lower()
            target = next((cid for cid, ctype in self._tries[-1] if ctype and ctype.lower() == wanted),
                          self._tries[-1][0][0])
            self.add_edge(node_id, target, "exception")
            return
        self._abnormal.append((node_id, "throws"))


# =====================================================================
# 3. KET XUAT MERMAID
# =====================================================================
_SAFE_LABEL_CHARS = set(" _.-?=!:,+*/%")


def _needs_quote(label):
    return any(not (c.isalnum() or c in _SAFE_LABEL_CHARS) for c in label or "")


def _escape(label):
    return (label or "(empty)").replace('"', "'").replace("\r", " ").replace("\n", " ")


def _declare(node):
    body = '"' + _escape(node["label"]) + '"' if _needs_quote(node["label"]) else _escape(node["label"])
    return "%s{%s}" % (node["id"], body) if node["kind"] == "DECISION" else "%s[%s]" % (node["id"], body)


def _sanitize_edge(label):
    return collapse("".join(c if (c.isalnum() or c in " _.") else " " for c in (label or "")))


def render_mermaid(nodes, edges):
    if not nodes:
        return "graph TD\n  A[Start] --> B[End]"

    lines = ["graph TD"]
    by_id = {n["id"]: n for n in nodes}

    groups = []
    for node in nodes:
        if not groups or groups[-1][0] != node["method"]:
            groups.append((node["method"], []))
        groups[-1][1].append(node)

    declared = set()
    if len(groups) > 1:
        for i, (method, items) in enumerate(groups):
            lines.append('  subgraph SG%d["%s"]' % (i, _escape(method or "code")))
            for node in items:
                lines.append("    " + _declare(node))
                declared.add(node["id"])
            lines.append("  end")

    def ref(node_id):
        if node_id in declared or node_id not in by_id:
            return node_id
        declared.add(node_id)
        return _declare(by_id[node_id])

    for edge in edges:
        src, dst = ref(edge["from"]), ref(edge["to"])
        label = _sanitize_edge(edge["label"])
        lines.append("  %s -- %s --> %s" % (src, label, dst) if label else "  %s --> %s" % (src, dst))

    for node in nodes:
        if node["id"] not in declared:
            lines.append("  " + _declare(node))
            declared.add(node["id"])

    return "\n".join(lines)


# =====================================================================
# 4. CRITICAL SNIPPETS
# =====================================================================
_DEPENDENCY_CALL = re.compile(r"^(await\s+)?[A-Za-z_]\w*(\s*\.\s*[A-Za-z_]\w*)+\s*\(")
_ASYNC_STMT = re.compile(r"\bawait\b|\b\w*Async\s*\(|\bCompletableFuture\b|\bTask\.Run\b"
                         r"|\.subscribe\s*\(|\bExecutorService\b")

_REASONS = {
    "BRANCH": "Nhanh dieu kien quyet dinh luong xu ly",
    "THROW": "Diem nem ngoai le - can sinh test case cho truong hop loi",
    "CATCH": "Khoi bat ngoai le - xac dinh hanh vi khi loi xay ra",
    "LOOP": "Vong lap - can kiem thu bien 0/1/n phan tu",
    "LOOP_COMPLEX": "Vong lap phuc tap (co re nhanh / lap long ben trong)",
    "SWITCH": "Re nhanh nhieu truong hop",
    "ASYNC": "Loi goi bat dong bo - anh huong thu tu thuc thi",
    "DEPENDENCY_CALL": "Loi goi sang thanh phan phu thuoc ben ngoai",
    "RETURN": "Diem thoat som ben trong nhanh dieu kien",
}


def _snippet_text(stmt):
    text = (stmt.get("text") or "").strip()
    if not text:
        return collapse(stmt.get("headerText"))
    if "\n" not in text and len(text) <= MAX_SNIPPET_LENGTH:
        return text
    first = text.split("\n")[0].rstrip("\r").strip() or collapse(stmt.get("headerText"))
    return first[:MAX_SNIPPET_LENGTH] + "..." if len(first) > MAX_SNIPPET_LENGTH else first


def _is_complex_loop(stmt):
    body = stmt.get("body") or []
    if len(body) >= 4:
        return True
    return any(c.get("kind") in ("If", "Switch", "While", "For", "ForEach", "DoWhile", "Try") for c in body)


def extract_snippets(methods, node_map):
    found = []
    state = {"hasComplexLoop": False}

    def add(stmt, kind, weight, method, reason_key=None):
        found.append({
            "code": _snippet_text(stmt),
            "nodeId": node_map.get(stmt.get("start"), ""),
            "kind": kind,
            "reason": _REASONS[reason_key or kind],
            "line": stmt.get("line", 0),
            "weight": weight,
            "method": method,
        })

    def walk(statements, depth, method):
        for stmt in statements or []:
            kind = stmt.get("kind")
            if kind == "If":
                add(stmt, "BRANCH", 9, method)
                walk(stmt.get("body"), depth + 1, method)
                walk(stmt.get("else"), depth + 1, method)
            elif kind == "Throw":
                add(stmt, "THROW", 10, method)
            elif kind == "Switch":
                add(stmt, "SWITCH", 8, method)
                for branch in stmt.get("cases", []):
                    walk(branch.get("body"), depth + 1, method)
            elif kind in ("While", "For", "ForEach", "DoWhile"):
                complex_loop = _is_complex_loop(stmt)
                if complex_loop:
                    state["hasComplexLoop"] = True
                add(stmt, "LOOP", 9 if complex_loop else 7, method,
                    "LOOP_COMPLEX" if complex_loop else "LOOP")
                walk(stmt.get("body"), depth + 1, method)
            elif kind == "Try":
                for clause in stmt.get("catches", []):
                    found.append({
                        "code": collapse(clause.get("headerText")),
                        "nodeId": node_map.get(clause.get("start"), ""),
                        "kind": "CATCH", "reason": _REASONS["CATCH"],
                        "line": clause.get("line", 0), "weight": 9, "method": method,
                    })
                    walk(clause.get("body"), depth + 1, method)
                walk(stmt.get("body"), depth + 1, method)
                walk(stmt.get("finally"), depth + 1, method)
            elif kind in ("Block", "Scoped"):
                walk(stmt.get("body"), depth + 1, method)
            elif kind == "Return":
                if depth > 0:
                    add(stmt, "RETURN", 6, method)
            else:
                text = stmt.get("text") or ""
                if _ASYNC_STMT.search(text):
                    add(stmt, "ASYNC", 8, method)
                elif _DEPENDENCY_CALL.match(text.lstrip()):
                    add(stmt, "DEPENDENCY_CALL", 5, method)

    for method in methods:
        walk(method.get("body"), 0, method.get("name", ""))

    # gop trung theo (dong, code), giu ban co trong so cao nhat
    unique = {}
    for item in found:
        key = (item["line"], item["code"])
        if key not in unique or unique[key]["weight"] < item["weight"]:
            unique[key] = item

    selected = sorted(sorted(unique.values(), key=lambda x: -x["weight"])[:MAX_SNIPPETS],
                      key=lambda x: x["line"])

    # bo snippet la chuoi con cua snippet khac
    result = [s for s in selected
              if not any(o is not s and len(o["code"]) > len(s["code"]) and s["code"] in o["code"]
                         for o in selected)]
    return result, state["hasComplexLoop"]


# =====================================================================
# 5. ENRICHED METADATA
# =====================================================================
_ASYNC_PATTERNS = [
    ("ASYNC_MODIFIER", r"\basync\b"),
    ("AWAIT", r"\bawait\b"),
    ("ASYNC_CALL", r"\b\w*Async\s*\("),
    ("TASK", r"\bTask\s*[<.]|\bValueTask\b|\bTask\.Run\b|\bConfigureAwait\b|\bGetAwaiter\b"),
    ("FUTURE", r"\bCompletableFuture\b|\bFuture\s*<|\bExecutorService\b|\bExecutors\.|\bnew\s+Thread\b|\bThreadPool\b"),
    ("ANNOTATION", r"@Async\b|@Scheduled\b|@EnableAsync\b"),
    ("REACTIVE", r"\bMono\s*<|\bFlux\s*<|\.subscribe\s*\(|\bObservable\b|\bIAsyncEnumerable\b"),
]
_IGNORED_RECEIVERS = {"this", "base", "super", "if", "for", "while", "switch", "return",
                      "new", "catch", "foreach", "using", "lock", "do", "else", "try"}


def extract_metadata(code, masked, methods, stats):
    source_lines = code.replace("\r\n", "\n").split("\n")
    masked_lines = masked.replace("\r\n", "\n").split("\n")

    markers = []
    for i, line in enumerate(masked_lines):
        for name, pattern in _ASYNC_PATTERNS:
            if re.search(pattern, line):
                text = source_lines[i].strip() if i < len(source_lines) else ""
                markers.append({"marker": name, "line": i + 1,
                                "code": text[:157] + "..." if len(text) > 160 else text})

    tags = []
    for m in re.finditer(r"@([A-Za-z_]\w*)", masked):
        tag = "@" + m.group(1)
        if tag not in tags:
            tags.append(tag)
    for m in re.finditer(r"(?m)^\s*\[\s*([A-Za-z_][\w.]*)", masked):
        tag = "[" + m.group(1) + "]"
        if tag not in tags:
            tags.append(tag)

    deps = {}

    def register(name, kind, member):
        if not name or name in _IGNORED_RECEIVERS:
            return
        entry = deps.setdefault(name, {"name": name, "kind": kind, "usageCount": 0, "members": []})
        entry["usageCount"] += 1
        if member and member not in entry["members"]:
            entry["members"].append(member)

    for m in re.finditer(r"([A-Za-z_]\w*)\s*\.\s*([A-Za-z_]\w*)\s*\(", masked):
        receiver = m.group(1)
        register(receiver, "STATIC_CALL" if receiver[0].isupper() else "FIELD_CALL", m.group(2))
    for m in re.finditer(r"\bnew\s+([A-Za-z_][\w.]*)\s*[(<]", masked):
        register(m.group(1).split(".")[-1], "INSTANTIATION", "")
    for method in methods:
        for param in _split_params(method.get("parameters", "")):
            tokens = [t for t in param.split(" ") if t]
            if len(tokens) < 2:
                continue
            type_name = re.sub(r"<.*?>|\[\]", "", tokens[-2])
            if type_name and type_name[0].isupper():
                register(type_name, "PARAMETER", "")

    thrown = []
    for m in re.finditer(r"throw\s+new\s+([A-Za-z_][\w.]*)", masked):
        name = m.group(1).split(".")[-1]
        if name not in thrown:
            thrown.append(name)
    for method in methods:
        for name in method.get("throwsTypes", []):
            if name not in thrown:
                thrown.append(name)

    caught = []
    for m in re.finditer(r"catch\s*\(\s*([A-Za-z_][\w.]*)", masked):
        name = m.group(1).split(".")[-1]
        if name not in caught:
            caught.append(name)

    dependencies = sorted(deps.values(), key=lambda d: (-d["usageCount"], d["name"]))
    is_async = bool(markers) or any(m.get("isAsync") for m in methods)

    metadata = {
        "isAsync": is_async,
        "asyncMarkers": markers,
        "annotationTags": tags,
        "dependencies": dependencies,
        "thrownExceptions": thrown,
        "caughtExceptions": caught,
        "methods": [{
            "name": m.get("name", ""), "returnType": m.get("returnType", ""),
            "parameters": m.get("parameters", ""), "modifiers": m.get("modifiers", []),
            "annotations": m.get("annotations", []), "isAsync": m.get("isAsync", False),
            "isStatic": m.get("isStatic", False), "startLine": m.get("startLine", 0),
        } for m in methods if m.get("signature") != "(code block)"],
        "controlFlow": stats,
        "tags": [],
    }
    metadata["tags"] = _build_tags(metadata, stats)
    return metadata


def _split_params(parameters):
    if not (parameters or "").strip():
        return []
    parts, depth, current = [], 0, ""
    for ch in parameters:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth <= 0:
            parts.append(current.strip())
            current = ""
            continue
        current += ch
    if current.strip():
        parts.append(current.strip())
    return parts


def _build_tags(metadata, stats):
    tags = []

    def add(tag):
        if tag not in tags:
            tags.append(tag)

    if metadata["isAsync"]:
        add("async")
    if stats["loopCount"]:
        add("has-loop")
    if stats["hasComplexLoop"]:
        add("complex-loop")
    if stats["branchCount"]:
        add("has-branch")
    if stats["throwCount"]:
        add("throws-exception")
    if stats["tryCatchCount"]:
        add("has-try-catch")
    if stats["switchCount"]:
        add("has-switch")
    if stats["maxNestingDepth"] >= 3:
        add("deep-nesting")
    if not metadata["dependencies"]:
        add("independent")

    for dep in metadata["dependencies"]:
        name = dep["name"].lower()
        members = " ".join(dep["members"]).lower()
        if "cache" in name or "cache" in members:
            add("cache")
        if any(k in name for k in ("repository", "repo", "dao", "dbcontext", "entitymanager")):
            add("database")
        if any(k in name for k in ("http", "client", "rest", "feign")):
            add("external-api")
        if any(k in name for k in ("mail", "email", "notif", "sms")):
            add("notification")
        if any(k in name for k in ("file", "stream", "io")):
            add("io")
        if "service" in name:
            add("service-call")

    for tag in metadata["annotationTags"]:
        lower = tag.lower()
        if "transactional" in lower:
            add("transactional")
        if "test" in lower:
            add("test-code")
        if "override" in lower:
            add("override")
        if any(k in lower for k in ("http", "mapping", "route")):
            add("endpoint")

    return tags


# =====================================================================
# 6. LAP RAP PROMPT
# =====================================================================
def compose_prompt(cfg_skeleton, metadata, snippets):
    parts = ["### 1. SYSTEM WORKFLOW GRAPH (CFG SKELETON)", "```mermaid", cfg_skeleton, "```", "",
             "### 2. CRITICAL DECISION LOGIC (GROUND TRUTH)"]

    chosen = [s for s in snippets if s["weight"] >= PROMPT_WEIGHT_THRESHOLD] or snippets
    if not chosen:
        parts.append("- (khong co nhanh dieu kien / ngoai le nao can giu nguyen van)")
    for snippet in chosen:
        anchor = "Node " + snippet["nodeId"] if snippet["nodeId"] else "Line %d" % snippet["line"]
        parts.append("- **%s:** %s" % (anchor, collapse(snippet["code"])))

    meta_lines = _metadata_lines(metadata)
    if meta_lines:
        parts += ["", "### 3. ENRICHED METADATA"] + meta_lines

    return "\n".join(parts).rstrip()


def _metadata_lines(metadata):
    lines = []
    for method in metadata["methods"][:5]:
        lines.append("- **Method:** %s(%s)%s%s%s" % (
            method["name"], method["parameters"],
            " : " + method["returnType"] if method["returnType"] else "",
            " `async`" if method["isAsync"] else "",
            " `static`" if method["isStatic"] else ""))

    if metadata["isAsync"]:
        seen, markers = set(), []
        for m in metadata["asyncMarkers"]:
            key = "%s@L%d" % (m["marker"], m["line"])
            if key not in seen:
                seen.add(key)
                markers.append(key)
        lines.append("- **Async:** YES (" + ", ".join(markers[:8]) + ")")

    if metadata["annotationTags"]:
        lines.append("- **Annotations:** " + ", ".join(metadata["annotationTags"][:15]))

    if metadata["dependencies"]:
        formatted = []
        for dep in metadata["dependencies"][:12]:
            members = "." + "/".join(dep["members"][:5]) if dep["members"] else ""
            formatted.append("%s%s (%s x%d)" % (dep["name"], members, dep["kind"], dep["usageCount"]))
        lines.append("- **Dependencies:** " + ", ".join(formatted))

    if metadata["thrownExceptions"]:
        lines.append("- **Throws:** " + ", ".join(metadata["thrownExceptions"][:10]))
    if metadata["caughtExceptions"]:
        lines.append("- **Catches:** " + ", ".join(metadata["caughtExceptions"][:10]))

    flow = metadata["controlFlow"]
    if sum(flow[k] for k in ("branchCount", "loopCount", "switchCount",
                             "tryCatchCount", "throwCount", "returnCount")) > 0:
        lines.append("- **Control flow:** branch=%d, loop=%d, switch=%d, try/catch=%d, "
                     "throw=%d, return=%d, maxNesting=%d" % (
                         flow["branchCount"], flow["loopCount"], flow["switchCount"],
                         flow["tryCatchCount"], flow["throwCount"], flow["returnCount"],
                         flow["maxNestingDepth"]))

    if metadata["tags"]:
        lines.append("- **Tags:** " + ", ".join(metadata["tags"][:20]))

    return lines


# =====================================================================
# 7. HAM CHINH
# =====================================================================
def _estimate_tokens(text):
    return 0 if not text else -(-len(text) // 4)      # ceil(len/4)


def _select_focus_methods(methods, module_id):
    """ModuleId dang 'Class.method' -> chi dung CFG cho dung ham do."""
    if len(methods) <= 1 or not (module_id or "").strip():
        return methods
    name = module_id.strip().split(".")[-1].split("(")[0].strip()
    if not name:
        return methods
    matched = [m for m in methods if (m.get("name") or "").lower() == name.lower()]
    return matched or methods


def _normalize_language(language):
    value = (language or "").strip().lower()
    if value == "java":
        return "java"
    if value in ("csharp", "c#", "cs", "dotnet", "net"):
        return "csharp"
    return value or "csharp"


def generate_hybrid_context(code, language, module_id="", routing_decision="ROUTE_HYBRID",
                            sloc=None, cyclomatic_complexity=None):
    started = time.perf_counter()
    normalized = _normalize_language(language)
    output = {
        "status": "PENDING",
        "route_decision": routing_decision or "ROUTE_HYBRID",
        "hybrid_prompt": "",
        "metrics": {},
        "moduleId": module_id or "",
        "language": normalized,
        "parser": "tree-sitter",
        "message": "",
        "cfgSkeleton": "",
        "criticalSnippets": [],
        "criticalSnippetDetails": [],
        "enrichedMetadata": {},
        "cfgNodes": [],
        "cfgEdges": [],
        "warnings": [],
        "processing_time_ms": 0,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
    }

    raw = code or ""
    if not raw.strip():
        output["status"] = "FAILED"
        output["message"] = "RawSourceCode rong - Tang 2 khong co du lieu de xay dung CFG."
        return output

    degraded = False
    if len(raw) > MAX_SOURCE_LENGTH:
        raw = raw[:MAX_SOURCE_LENGTH]
        output["warnings"].append("Ma nguon vuot qua %d ky tu nen da bi cat bot." % MAX_SOURCE_LENGTH)
        degraded = True

    if (routing_decision or "").upper() != "ROUTE_HYBRID":
        output["warnings"].append(
            "RoutingDecision = '%s' (khong phai ROUTE_HYBRID) - van xu ly de phuc vu kiem thu."
            % routing_decision)

    if normalized not in ("java", "csharp"):
        output["warnings"].append(
            "Ngon ngu '%s' khong nam trong danh sach ho tro (java, csharp)." % language)
        degraded = True

    # ── 1. Cay cau lenh tu tree-sitter (in-memory) ───────────────
    structure = parse_structure(raw, normalized)
    masked = mask_source(raw, normalized)
    methods = structure.get("methods", [])

    for warning in structure.get("warnings", []):
        output["warnings"].append(warning)
    if structure.get("hasError"):
        output["warnings"].append("tree-sitter bao cay cu phap co node loi - CFG la best-effort.")
        degraded = True
    if not methods:
        output["status"] = "FAILED"
        output["message"] = "Khong phan tich duoc cau truc ma nguon."
        output["processing_time_ms"] = int((time.perf_counter() - started) * 1000)
        return output

    # ── 2. Chon ham muc tieu ─────────────────────────────────────
    focus = _select_focus_methods(methods, module_id)
    if len(focus) < len(methods):
        output["warnings"].append(
            "ModuleId tro toi ham '%s' - CFG chi dung cho ham nay (%d ham khac chi nam trong metadata)."
            % (focus[0].get("name"), len(methods) - len(focus)))

    # ── 3. CFG ───────────────────────────────────────────────────
    builder = CfgBuilder()
    for method in focus:
        builder.build_method(method)

    if builder.truncated:
        output["warnings"].append("CFG qua lon nen da duoc rut gon (gioi han so node).")
        degraded = True

    output["cfgNodes"] = builder.nodes
    output["cfgEdges"] = builder.edges
    output["cfgSkeleton"] = render_mermaid(builder.nodes, builder.edges)

    # ── 4. Critical snippets ─────────────────────────────────────
    snippets, has_complex_loop = extract_snippets(focus, builder.stmt_node_map)
    builder.stats["hasComplexLoop"] = has_complex_loop
    output["criticalSnippetDetails"] = snippets
    output["criticalSnippets"] = [s["code"] for s in snippets]

    # ── 5. Enriched metadata ─────────────────────────────────────
    output["enrichedMetadata"] = extract_metadata(raw, masked, methods, builder.stats)

    # ── 6. Prompt ────────────────────────────────────────────────
    output["hybrid_prompt"] = compose_prompt(output["cfgSkeleton"],
                                             output["enrichedMetadata"], snippets)

    # ── 7. Chi so do luong ───────────────────────────────────────
    if sloc is None or cyclomatic_complexity is None:
        ext = "java" if normalized == "java" else "cs"
        _, vg_router, sloc_router, _ = analyze_ast(raw, ext)
        sloc = sloc_router if sloc is None else sloc
        cyclomatic_complexity = vg_router if cyclomatic_complexity is None else cyclomatic_complexity

    original_tokens = _estimate_tokens(raw)
    hybrid_tokens = _estimate_tokens(output["hybrid_prompt"])
    output["metrics"] = {
        "original_sloc": sloc,
        "cyclomatic_complexity": cyclomatic_complexity,
        "estimated_token_saving_pct": (round((1 - hybrid_tokens / original_tokens) * 100, 1)
                                       if original_tokens else 0),
        "cyclomatic_complexity_cfg": max(1, len(builder.edges) - len(builder.nodes) + 2 * max(1, len(focus))),
        "original_tokens": original_tokens,
        "hybrid_tokens": hybrid_tokens,
        "cfg_node_count": len(builder.nodes),
        "cfg_edge_count": len(builder.edges),
        "method_count": len(focus),
        "critical_snippet_count": len(snippets),
    }

    output["status"] = "PARTIAL" if degraded else "SUCCESS"
    output["message"] = (
        "Da sinh ngu canh lai cho module '%s': %d node / %d canh CFG, %d critical snippet, "
        "%d phu thuoc, V(G) tu CFG = %d, tiet kiem ~%s%% token." % (
            output["moduleId"], len(builder.nodes), len(builder.edges), len(snippets),
            len(output["enrichedMetadata"]["dependencies"]),
            output["metrics"]["cyclomatic_complexity_cfg"],
            output["metrics"]["estimated_token_saving_pct"]))
    output["processing_time_ms"] = int((time.perf_counter() - started) * 1000)
    return output
