# -*- coding: utf-8 -*-
"""
TANG 2 - Bo chuyen cay cu phap tree-sitter thanh cay cau lenh chuan hoa.

Muc dich: thay the bo parser tu viet trong C# (CodeStructureParser) bang tree-sitter,
nhung GIU NGUYEN mo hinh du lieu, de CfgBuilder / CriticalSnippetExtractor /
EnrichedMetadataExtractor ben .NET khong phai sua mot dong nao.

Ket qua tra ve dung dang ma StructureServiceClient.cs mong doi:

{
  "ok": true, "parser": "tree-sitter", "hasError": false, "language": "java",
  "methods": [{
      "name", "parameters", "returnType", "signature", "modifiers", "annotations",
      "throwsTypes", "startLine", "isAsync", "isStatic",
      "body": [ <stmt>, ... ]
  }],
  "warnings": []
}

<stmt> = {
  "kind": Simple|Declaration|Return|Throw|Break|Continue|If|While|For|ForEach|
          DoWhile|Switch|Try|Block|Scoped,
  "start", "end", "line", "text", "headerText", "condition", "forInit", "forUpdate",
  "body": [...], "else": [...], "finally": [...],
  "catches": [{"start","line","parameter","exceptionType","headerText","body"}],
  "cases":   [{"label","isDefault","line","body"}]
}

KHONG sua main.py / ast_analyzer.py / requirements.txt.
"""
import re

import tree_sitter
import tree_sitter_java as tsjava
import tree_sitter_c_sharp as tscsharp

LANGUAGES = {
    "java": tree_sitter.Language(tsjava.language()),
    "cs": tree_sitter.Language(tscsharp.language()),
}
PARSERS = {ext: tree_sitter.Parser(lang) for ext, lang in LANGUAGES.items()}

EXT_MAP = {
    "java": "java",
    "csharp": "cs", "c#": "cs", "cs": "cs", "dotnet": "cs", "net": "cs",
}

MAX_DEPTH = 40

# ── Cac loai node khai bao ham ───────────────────────────────────────
METHOD_NODES = {
    "method_declaration", "constructor_declaration",
    "compact_constructor_declaration", "local_function_statement",
}

# ── Modifier / annotation ────────────────────────────────────────────
MODIFIER_WORDS = {
    "public", "private", "protected", "internal", "static", "final", "abstract",
    "virtual", "override", "sealed", "async", "extern", "unsafe", "partial",
    "native", "synchronized", "transient", "volatile", "readonly", "const",
    "strictfp", "new", "implicit", "explicit",
}


class _Src:
    """Anh xa offset byte cua tree-sitter -> offset ky tu cua chuoi Python."""

    def __init__(self, code: str):
        self.code = code
        self.data = code.encode("utf8")
        # byte_to_char[i] = so ky tu truoc byte thu i
        self.byte_to_char = [0] * (len(self.data) + 1)
        char_i = 0
        byte_i = 0
        for ch in code:
            n = len(ch.encode("utf8"))
            for k in range(n):
                self.byte_to_char[byte_i + k] = char_i
            byte_i += n
            char_i += 1
        self.byte_to_char[len(self.data)] = char_i

    def ch(self, byte_offset: int) -> int:
        if byte_offset < 0:
            return 0
        if byte_offset >= len(self.byte_to_char):
            return self.byte_to_char[-1]
        return self.byte_to_char[byte_offset]

    def text(self, node) -> str:
        return self.code[self.ch(node.start_byte):self.ch(node.end_byte)]


def _collapse(text: str) -> str:
    return re.sub(r"\s+", " ", text or "").strip()


def _strip_parens(text: str) -> str:
    t = (text or "").strip()
    if t.startswith("(") and t.endswith(")"):
        t = t[1:-1]
    return t.strip()


def _new_stmt(kind, src, node):
    return {
        "kind": kind,
        "start": src.ch(node.start_byte),
        "end": src.ch(node.end_byte),
        "line": node.start_point[0] + 1,
        "text": src.text(node).strip(),
        "headerText": "",
        "condition": "",
        "forInit": "",
        "forUpdate": "",
        "body": [],
        "else": [],
        "catches": [],
        "finally": [],
        "cases": [],
    }


def _field_text(src, node, *names):
    for name in names:
        child = node.child_by_field_name(name)
        if child is not None:
            return src.text(child)
    return ""


def _field(node, *names):
    for name in names:
        child = node.child_by_field_name(name)
        if child is not None:
            return child
    return None


def _header_until_body(src, node, body_node):
    """Lay phan dau cau lenh: tu dau node den truoc than cua no."""
    end = body_node.start_byte if body_node is not None else node.end_byte
    return _collapse(src.code[src.ch(node.start_byte):src.ch(end)])


# ── Chuyen mot node cau lenh ─────────────────────────────────────────
def _convert_stmt(src, node, depth):
    if depth > MAX_DEPTH:
        return None

    t = node.type

    # -------- Khoi lenh --------
    if t == "block":
        stmt = _new_stmt("Block", src, node)
        stmt["body"] = _convert_block(src, node, depth + 1)
        return stmt

    # -------- if / else --------
    if t == "if_statement":
        stmt = _new_stmt("If", src, node)
        cond = _field(node, "condition")
        stmt["condition"] = _collapse(_strip_parens(src.text(cond))) if cond is not None else ""
        consequence = _field(node, "consequence")
        alternative = _field(node, "alternative")
        stmt["headerText"] = _header_until_body(src, node, consequence)
        stmt["body"] = _as_list(src, consequence, depth + 1)
        stmt["else"] = _as_list(src, alternative, depth + 1)
        return stmt

    # -------- while --------
    if t == "while_statement":
        stmt = _new_stmt("While", src, node)
        cond = _field(node, "condition")
        stmt["condition"] = _collapse(_strip_parens(src.text(cond))) if cond is not None else ""
        body = _field(node, "body", "statement")
        stmt["headerText"] = _header_until_body(src, node, body)
        stmt["body"] = _as_list(src, body, depth + 1)
        return stmt

    # -------- for co 3 phan --------
    if t == "for_statement":
        stmt = _new_stmt("For", src, node)
        stmt["forInit"] = _collapse(_field_text(src, node, "initializer", "init")).rstrip(";")
        stmt["condition"] = _collapse(_field_text(src, node, "condition")).rstrip(";")
        stmt["forUpdate"] = _collapse(_field_text(src, node, "update"))
        body = _field(node, "body", "statement")
        stmt["headerText"] = _header_until_body(src, node, body)
        stmt["body"] = _as_list(src, body, depth + 1)
        return stmt

    # -------- foreach (Java: enhanced_for_statement | C#: for_each_statement) --------
    if t in ("enhanced_for_statement", "for_each_statement", "foreach_statement"):
        stmt = _new_stmt("ForEach", src, node)
        body = _field(node, "body", "statement")
        header = _header_until_body(src, node, body)
        inner = re.search(r"\((.*)\)\s*$", header, re.S)
        stmt["condition"] = _collapse(inner.group(1)) if inner else header
        stmt["headerText"] = header
        stmt["body"] = _as_list(src, body, depth + 1)
        return stmt

    # -------- do ... while --------
    if t == "do_statement":
        stmt = _new_stmt("DoWhile", src, node)
        cond = _field(node, "condition")
        stmt["condition"] = _collapse(_strip_parens(src.text(cond))) if cond is not None else ""
        body = _field(node, "body", "statement")
        stmt["headerText"] = "do ... while (" + stmt["condition"] + ")"
        stmt["body"] = _as_list(src, body, depth + 1)
        return stmt

    # -------- switch --------
    if t in ("switch_statement", "switch_expression"):
        stmt = _new_stmt("Switch", src, node)
        selector = _field(node, "condition", "value")
        stmt["condition"] = _collapse(_strip_parens(src.text(selector))) if selector is not None else ""
        body = _field(node, "body")
        stmt["headerText"] = _header_until_body(src, node, body)
        stmt["cases"] = _convert_switch_body(src, body, depth + 1)
        return stmt

    # -------- try / catch / finally --------
    if t in ("try_statement", "try_with_resources_statement"):
        stmt = _new_stmt("Try", src, node)
        stmt["headerText"] = "try"
        resources = _field(node, "resources")
        if resources is not None:
            stmt["condition"] = _collapse(_strip_parens(src.text(resources)))
        body = _field(node, "body", "block")
        if body is None:
            for child in node.named_children:
                if child.type == "block":
                    body = child
                    break
        stmt["body"] = _convert_block(src, body, depth + 1) if body is not None else []

        for child in node.named_children:
            if child.type == "catch_clause":
                param_node = _field(child, "parameter", "declaration")
                if param_node is None:
                    for c in child.named_children:
                        if c.type in ("catch_formal_parameter", "catch_declaration"):
                            param_node = c
                            break
                parameter = _collapse(_strip_parens(src.text(param_node))) if param_node is not None else ""
                m = re.search(r"([A-Za-z_][\w.]*)", parameter)
                catch_body = _field(child, "body")
                if catch_body is None:
                    for c in child.named_children:
                        if c.type == "block":
                            catch_body = c
                stmt["catches"].append({
                    "start": src.ch(child.start_byte),
                    "line": child.start_point[0] + 1,
                    "parameter": parameter,
                    "exceptionType": (m.group(1).split(".")[-1] if m else "Exception"),
                    "headerText": ("catch" if not parameter else "catch (" + parameter + ")"),
                    "body": _convert_block(src, catch_body, depth + 1) if catch_body is not None else [],
                })
            elif child.type == "finally_clause":
                fin = _field(child, "body")
                if fin is None:
                    for c in child.named_children:
                        if c.type == "block":
                            fin = c
                stmt["finally"] = _convert_block(src, fin, depth + 1) if fin is not None else []
        return stmt

    # -------- using / lock / synchronized / fixed --------
    if t in ("using_statement", "lock_statement", "synchronized_statement", "fixed_statement"):
        stmt = _new_stmt("Scoped", src, node)
        body = _field(node, "body")
        if body is None:
            for c in node.named_children:
                if c.type in ("block", "expression_statement"):
                    body = c
        header = _header_until_body(src, node, body)
        stmt["headerText"] = header
        inner = re.search(r"\((.*)\)\s*$", header, re.S)
        stmt["condition"] = _collapse(inner.group(1)) if inner else ""
        stmt["body"] = _as_list(src, body, depth + 1)
        return stmt

    # -------- cac lenh nhay --------
    if t == "throw_statement":
        return _new_stmt("Throw", src, node)
    if t == "return_statement":
        return _new_stmt("Return", src, node)
    if t == "break_statement":
        return _new_stmt("Break", src, node)
    if t == "continue_statement":
        return _new_stmt("Continue", src, node)

    # -------- nhan (labeled) -> lot vo lay lenh ben trong --------
    if t in ("labeled_statement", "labeled_expression"):
        for c in node.named_children:
            if c.type not in ("identifier",):
                return _convert_stmt(src, c, depth)
        return None

    # -------- khai bao bien --------
    if t in ("local_variable_declaration", "local_declaration_statement", "field_declaration"):
        return _new_stmt("Declaration", src, node)

    # -------- ham cuc bo: bo qua, khong dua vao CFG cua ham cha --------
    if t == "local_function_statement":
        return None

    # -------- con lai: cau lenh thuong --------
    if t.endswith("_statement") or t in ("expression_statement",):
        return _new_stmt("Simple", src, node)

    return None


def _as_list(src, node, depth):
    """Mot nhanh co the la block hoac mot cau lenh don."""
    if node is None:
        return []
    if node.type == "block":
        return _convert_block(src, node, depth)
    stmt = _convert_stmt(src, node, depth)
    return [stmt] if stmt else []


def _convert_block(src, block_node, depth):
    if block_node is None:
        return []
    out = []
    for child in block_node.named_children:
        if child.type in ("comment", "line_comment", "block_comment"):
            continue
        stmt = _convert_stmt(src, child, depth)
        if stmt:
            out.append(stmt)
    return out


def _convert_switch_body(src, body_node, depth):
    """Java: switch_block_statement_group / switch_rule. C#: switch_section."""
    cases = []
    if body_node is None:
        return cases

    for group in body_node.named_children:
        if group.type in ("comment", "line_comment", "block_comment"):
            continue

        labels, stmts = [], []
        for child in group.named_children:
            ct = child.type
            if ct in ("switch_label", "case_switch_label", "default_switch_label",
                      "switch_rule_label", "case_pattern_switch_label"):
                labels.append(src.text(child))
            elif ct in ("comment", "line_comment", "block_comment"):
                continue
            else:
                stmt = _convert_stmt(src, child, depth)
                if stmt:
                    stmts.append(stmt)

        if not labels:
            # Java 14 switch_rule: "case X ->" nam o field label
            lab = _field(group, "label")
            if lab is not None:
                labels.append(src.text(lab))

        for i, raw in enumerate(labels):
            text = _collapse(raw).rstrip(":").strip()
            is_default = text.startswith("default")
            if not is_default:
                text = re.sub(r"^case\s*", "", text).strip()
            cases.append({
                "label": "default" if is_default else text,
                "isDefault": is_default,
                "line": group.start_point[0] + 1,
                # nhieu nhan lien tiep (fall-through): chi nhan cuoi giu than lenh
                "body": stmts if i == len(labels) - 1 else [],
            })
    return cases


# ── Khai bao ham ─────────────────────────────────────────────────────
def _method_info(src, node, lang):
    name_node = _field(node, "name")
    name = src.text(name_node) if name_node is not None else ""

    params_node = _field(node, "parameters", "parameter_list")
    parameters = _collapse(_strip_parens(src.text(params_node))) if params_node is not None else ""

    type_node = _field(node, "type", "returns", "return_type")
    return_type = _collapse(src.text(type_node)) if type_node is not None else ""

    modifiers, annotations = [], []
    for child in node.children:
        if child.type in ("modifiers", "modifier"):
            for word in re.findall(r"[A-Za-z_]\w*", src.text(child)):
                if word in MODIFIER_WORDS and word not in modifiers:
                    modifiers.append(word)
            for ann in re.findall(r"@\w+", src.text(child)):
                if ann not in annotations:
                    annotations.append(ann)
        elif child.type == "attribute_list":
            for attr in re.findall(r"[A-Za-z_][\w.]*", src.text(child)):
                tag = "[" + attr + "]"
                if tag not in annotations:
                    annotations.append(tag)

    throws_types = []
    throws_node = _field(node, "throws")
    if throws_node is not None:
        throws_types = [t.strip() for t in
                        re.sub(r"^throws\s*", "", _collapse(src.text(throws_node))).split(",") if t.strip()]

    body_node = _field(node, "body")
    signature = _collapse(
        (" ".join(modifiers) + " " if modifiers else "")
        + (return_type + " " if return_type else "")
        + name + "(" + parameters + ")")

    return {
        "name": name,
        "parameters": parameters,
        "returnType": return_type,
        "signature": signature,
        "modifiers": modifiers,
        "annotations": annotations,
        "throwsTypes": throws_types,
        "startLine": node.start_point[0] + 1,
        "isAsync": "async" in modifiers,
        "isStatic": "static" in modifiers,
        "body": _convert_block(src, body_node, 1) if body_node is not None else [],
    }


def _find_methods(src, root, lang):
    """Duyet tu tren xuong, gap ham thi lay roi KHONG di sau vao than ham nua."""
    methods = []

    def walk(node):
        if node.type in METHOD_NODES:
            if node.type != "local_function_statement":
                methods.append(_method_info(src, node, lang))
            return
        for child in node.children:
            walk(child)

    walk(root)
    return methods


# ── API chinh ────────────────────────────────────────────────────────
# Doan code chi gom mot ham roi (khong nam trong class) khong phai la don vi bien dich
# hop le cua Java/C#, tree-sitter se bao loi. Boc tam vao mot class ao roi tru lai offset.
# Tien to KHONG chua ky tu xuong dong nen so dong giu nguyen.
_WRAP_PREFIX = "class __CfgWrapper { "
_WRAP_SUFFIX = " }"


def _shift_offsets(node, delta):
    """Tru lai phan offset do tien to class ao gay ra."""
    if isinstance(node, dict):
        for key in ("start", "end"):
            if key in node and isinstance(node[key], int):
                node[key] = max(0, node[key] - delta)
        for value in node.values():
            _shift_offsets(value, delta)
    elif isinstance(node, list):
        for item in node:
            _shift_offsets(item, delta)


def parse_structure(code: str, language: str) -> dict:
    # 1. Phan tich truc tiep - uu tien nhat
    result = _parse_once(code, language, allow_synthetic=False)
    if result["methods"]:
        return result

    # 2. Doan code chi la mot ham roi -> boc vao class ao roi thu lai
    wrapped = _parse_once(_WRAP_PREFIX + (code or "") + _WRAP_SUFFIX, language,
                          allow_synthetic=False)
    if wrapped["methods"]:
        _shift_offsets(wrapped["methods"], len(_WRAP_PREFIX))
        wrapped["hasError"] = False
        return wrapped

    # 3. Khong ra ham nao -> coi ca doan la mot than ham gia lap
    return _parse_once(code, language, allow_synthetic=True)


def _parse_once(code: str, language: str, allow_synthetic: bool = True) -> dict:
    lang_key = EXT_MAP.get((language or "").strip().lower(), "cs")
    parser = PARSERS[lang_key]

    src = _Src(code or "")
    tree = parser.parse(src.data)
    root = tree.root_node

    warnings = []
    if root.has_error:
        warnings.append("Cay cu phap co node loi - ket qua la best-effort.")

    methods = _find_methods(src, root, lang_key)

    if not methods and allow_synthetic:
        # Ma nguon chi la mot doan lenh roi -> coi ca doan la mot than ham gia lap
        body = []
        for child in root.named_children:
            if child.type in ("comment", "line_comment", "block_comment"):
                continue
            stmt = _convert_stmt(src, child, 1)
            if stmt:
                body.append(stmt)
        if body:
            methods = [{
                "name": "code", "parameters": "", "returnType": "", "signature": "(code block)",
                "modifiers": [], "annotations": [], "throwsTypes": [], "startLine": 1,
                "isAsync": False, "isStatic": False, "body": body,
            }]
            warnings.append("Khong tim thay khai bao ham - dung CFG tren toan bo doan ma nguon.")

    return {
        "ok": True,
        "parser": "tree-sitter",
        "language": "java" if lang_key == "java" else "csharp",
        "hasError": bool(root.has_error),
        "methods": methods,
        "warnings": warnings,
    }


# ── Che comment / chuoi bang chinh node type cua tree-sitter ─────────
# Dung cho buoc quet metadata (async marker, annotation, dependency).
# Khong dung bo quet chuoi tu viet -> khong dinh cac bay "chuoi chua tu khoa".
MASK_NODE_TYPES = {
    "comment", "line_comment", "block_comment", "documentation_comment",
    "string_literal", "character_literal", "text_block", "raw_string_literal",
    "verbatim_string_literal", "interpolated_string_expression",
    "interpolated_verbatim_string_text", "string_content", "char_literal",
}


def mask_source(code: str, language: str) -> str:
    """Tra ve ban sao cua ma nguon voi comment va chuoi bi thay bang dau cach
    (giu nguyen do dai va so dong, nen moi chi so van anh xa 1-1)."""
    lang_key = EXT_MAP.get((language or "").strip().lower(), "cs")
    parser = PARSERS[lang_key]

    src = _Src(code or "")
    tree = parser.parse(src.data)
    buf = list(src.code)

    def walk(node):
        if node.type in MASK_NODE_TYPES:
            for i in range(src.ch(node.start_byte), src.ch(node.end_byte)):
                if i < len(buf) and buf[i] != "\n":
                    buf[i] = " "
            return
        for child in node.children:
            walk(child)

    walk(tree.root_node)
    return "".join(buf)
