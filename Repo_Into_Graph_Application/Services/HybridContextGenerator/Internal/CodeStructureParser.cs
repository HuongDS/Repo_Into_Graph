using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Bo phan tich cau truc ma nguon Java / C# (khong dung compiler ngoai).
    /// Tra ve danh sach ham va cay cau lenh rut gon du de dung CFG.
    ///
    /// Nguyen tac an toan:
    ///  - Moi vong lap deu co bo dem (_guard) chan lap vo han.
    ///  - Moi buoc deu bat buoc con tro tien len it nhat 1 ky tu.
    /// </summary>
    internal sealed class CodeStructureParser
    {
        private const int MaxIterations = 300_000;
        private const int MaxNestingLevel = 40;

        private readonly SourceScanner _sc;
        private int _guard;
        private int _level;

        /// <summary>Cac tu khoa dieu khien - khong duoc coi la ten ham.</summary>
        private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal)
        {
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
            "try", "catch", "finally", "return", "throw", "throws", "new", "using",
            "lock", "synchronized", "fixed", "unsafe", "checked", "unchecked", "yield",
            "goto", "break", "continue", "assert", "where", "when", "is", "as", "in",
            "out", "ref", "typeof", "sizeof", "nameof", "instanceof", "record", "class",
            "interface", "enum", "struct", "namespace", "package", "import", "get", "set",
            "add", "remove", "value", "this", "base", "super", "await", "select", "from"
        };

        private static readonly HashSet<string> ModifierKeywords = new(StringComparer.Ordinal)
        {
            "public", "private", "protected", "internal", "static", "final", "abstract",
            "virtual", "override", "sealed", "async", "extern", "unsafe", "partial",
            "native", "synchronized", "transient", "volatile", "readonly", "new", "const",
            "default", "strictfp", "implicit", "explicit", "operator"
        };

        public CodeStructureParser(SourceScanner scanner)
        {
            _sc = scanner;
        }

        // -------------------------------------------------------------
        // 1. TIM CAC HAM TRONG MA NGUON
        // -------------------------------------------------------------
        public List<MethodDecl> ExtractMethods()
        {
            var methods = new List<MethodDecl>();
            string masked = _sc.Masked;
            int n = masked.Length;
            int i = 0;

            while (i < n)
            {
                if (++_guard > MaxIterations) break;

                if (masked[i] != '(') { i++; continue; }

                int close = _sc.FindMatching(i, n);
                if (close < 0) { i++; continue; }

                int after = _sc.SkipTrivia(close + 1, n);

                // Bo qua "throws A, B" (Java) hoac rang buoc generic "where T : class" (C#)
                var throwsTypes = new List<string>();
                string nextWord = _sc.PeekWord(after, out int nextEnd);
                if (nextWord == "throws" || nextWord == "where")
                {
                    int j = nextEnd;
                    while (j < n && masked[j] != '{' && masked[j] != ';') j++;
                    if (nextWord == "throws")
                    {
                        foreach (var t in _sc.Slice(nextEnd, j).Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var throwsType = t.Trim();
                            if (throwsType.Length > 0) throwsTypes.Add(throwsType);
                        }
                    }
                    after = _sc.SkipTrivia(j, n);
                }

                // Phai co than ham '{' ngay sau -> neu khong thi day chi la loi goi ham
                if (after >= n || masked[after] != '{') { i = close + 1; continue; }

                // Ten ham nam ngay truoc dau '('
                int nameEnd = _sc.SkipTriviaBack(i - 1);
                if (nameEnd < 0) { i = close + 1; continue; }
                if (!SourceScanner.IsIdentChar(masked[nameEnd])) { i = close + 1; continue; }

                int nameStart = nameEnd;
                while (nameStart >= 0 && SourceScanner.IsIdentChar(masked[nameStart])) nameStart--;
                nameStart++;

                string name = _sc.Slice(nameStart, nameEnd + 1);
                if (name.Length == 0 || ControlKeywords.Contains(name)) { i = close + 1; continue; }

                // Vung tien to (modifier + kieu tra ve + annotation)
                int declStart = FindDeclarationStart(nameStart - 1);
                string prefixRaw = _sc.Slice(declStart, nameStart);
                string prefixTrimmed = prefixRaw.TrimEnd();

                // Loai bo: loi goi phuong thuc (obj.method(...)), khoi tao vo danh (new X(){...})
                if (prefixTrimmed.EndsWith(".", StringComparison.Ordinal) ||
                    prefixTrimmed.EndsWith("=", StringComparison.Ordinal) ||
                    prefixTrimmed.EndsWith("->", StringComparison.Ordinal) ||
                    prefixTrimmed.EndsWith("=>", StringComparison.Ordinal) ||
                    Regex.IsMatch(prefixTrimmed, @"\bnew\s*$"))
                {
                    i = close + 1;
                    continue;
                }

                // Phai co it nhat mot tu (modifier hoac kieu tra ve) dung truoc ten ham,
                // tru truong hop constructor "Name(...) {" nam ngay sau '{' cua class.
                var prefixTokens = Tokenize(StripAnnotations(prefixTrimmed));
                if (prefixTokens.Count == 0 && !LooksLikeConstructor(declStart))
                {
                    i = close + 1;
                    continue;
                }

                int bodyEnd = _sc.FindMatching(after, n);
                if (bodyEnd < 0) bodyEnd = n - 1;

                var method = new MethodDecl
                {
                    Name = name,
                    Parameters = SourceScanner.Collapse(_sc.Slice(i + 1, close)),
                    BodyStart = after + 1,
                    BodyEnd = bodyEnd,
                    StartLine = _sc.LineOf(declStart),
                    Annotations = ExtractAnnotations(prefixRaw),
                    ThrowsTypes = throwsTypes
                };

                foreach (var token in prefixTokens)
                {
                    if (ModifierKeywords.Contains(token)) method.Modifiers.Add(token);
                    else method.ReturnType = string.IsNullOrEmpty(method.ReturnType) ? token : method.ReturnType + " " + token;
                }

                method.IsAsync = method.Modifiers.Contains("async");
                method.IsStatic = method.Modifiers.Contains("static");
                method.Signature = SourceScanner.Collapse(
                    (method.Modifiers.Count > 0 ? string.Join(" ", method.Modifiers) + " " : string.Empty) +
                    (string.IsNullOrEmpty(method.ReturnType) ? string.Empty : method.ReturnType + " ") +
                    name + "(" + method.Parameters + ")");

                methods.Add(method);

                // Nhay qua than ham de khong nham lambda / local function ben trong
                i = bodyEnd + 1;
            }

            return methods;
        }

        /// <summary>Lui ve dau khai bao: sau dau ; { } gan nhat.</summary>
        private int FindDeclarationStart(int from)
        {
            string masked = _sc.Masked;
            int i = from;
            while (i >= 0)
            {
                char c = masked[i];
                if (c == ';' || c == '{' || c == '}') return i + 1;
                i--;
            }
            return 0;
        }

        private bool LooksLikeConstructor(int declStart)
        {
            int p = _sc.SkipTriviaBack(declStart - 1);
            return p >= 0 && _sc.Masked[p] == '{';
        }

        private static string StripAnnotations(string text)
        {
            string result = Regex.Replace(text, @"@\w+(\s*\([^)]*\))?", " ");
            result = Regex.Replace(result, @"\[[^\]\[]*\]", " ");
            return result;
        }

        private static List<string> Tokenize(string text)
        {
            return SourceScanner.Collapse(text)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 0)
                .ToList();
        }

        private static List<string> ExtractAnnotations(string prefix)
        {
            var list = new List<string>();
            foreach (Match m in Regex.Matches(prefix, @"@\w+"))
            {
                if (!list.Contains(m.Value)) list.Add(m.Value);
            }
            foreach (Match m in Regex.Matches(prefix, @"\[\s*([A-Za-z_][\w.]*)"))
            {
                string tag = "[" + m.Groups[1].Value + "]";
                if (!list.Contains(tag)) list.Add(tag);
            }
            return list;
        }

        // -------------------------------------------------------------
        // 2. PHAN TICH CAU LENH
        // -------------------------------------------------------------
        public List<Stmt> ParseStatements(int start, int end)
        {
            var list = new List<Stmt>();
            if (_level > MaxNestingLevel) return list;

            _level++;
            int pos = start;
            while (pos < end)
            {
                if (++_guard > MaxIterations) break;

                pos = _sc.SkipTrivia(pos, end);
                if (pos >= end) break;

                int before = pos;
                Stmt? stmt = ParseOne(ref pos, end);
                if (stmt != null) list.Add(stmt);
                if (pos <= before) pos = before + 1; // bat buoc tien len
            }
            _level--;
            return list;
        }

        private Stmt? ParseOne(ref int pos, int limit)
        {
            string masked = _sc.Masked;
            pos = _sc.SkipTrivia(pos, limit);
            if (pos >= limit) return null;

            char c = masked[pos];

            if (c == ';') { pos++; return null; }
            if (c == '}') { pos++; return null; }

            if (c == '{')
            {
                int close = _sc.FindMatching(pos, limit);
                if (close < 0) { pos = limit; return null; }
                var block = NewStmt(StmtKind.Block, pos, close + 1);
                block.Body = ParseStatements(pos + 1, close);
                pos = close + 1;
                return block;
            }

            string word = _sc.PeekWord(pos, out int wordEnd);

            switch (word)
            {
                case "if":
                    return ParseIf(ref pos, wordEnd, limit);
                case "while":
                    return ParseWhile(ref pos, wordEnd, limit);
                case "for":
                case "foreach":
                    return ParseFor(ref pos, wordEnd, limit, word == "foreach");
                case "do":
                    return ParseDoWhile(ref pos, wordEnd, limit);
                case "switch":
                    return ParseSwitch(ref pos, wordEnd, limit);
                case "try":
                    return ParseTry(ref pos, wordEnd, limit);
                case "using":
                case "lock":
                case "synchronized":
                case "fixed":
                    {
                        int p = _sc.SkipTrivia(wordEnd, limit);
                        if (p < limit && masked[p] == '(') return ParseScoped(ref pos, wordEnd, limit);
                        return ParseSimple(ref pos, limit);
                    }
                default:
                    return ParseSimple(ref pos, limit);
            }
        }

        private Stmt NewStmt(StmtKind kind, int start, int end)
        {
            return new Stmt
            {
                Kind = kind,
                Start = start,
                End = end,
                Line = _sc.LineOf(start),
                Text = _sc.Slice(start, end).Trim()
            };
        }

        private Stmt ParseSimple(ref int pos, int limit)
        {
            int start = pos;
            int end = FindSimpleEnd(start, limit);
            if (end <= start) end = Math.Min(start + 1, limit);

            var stmt = NewStmt(StmtKind.Simple, start, end);
            string firstWord = _sc.PeekWord(start, out _);

            stmt.Kind = firstWord switch
            {
                "return" => StmtKind.Return,
                "throw" => StmtKind.Throw,
                "break" => StmtKind.Break,
                "continue" => StmtKind.Continue,
                _ => LooksLikeDeclaration(stmt.Text) ? StmtKind.Declaration : StmtKind.Simple
            };

            pos = end;
            return stmt;
        }

        private static bool LooksLikeDeclaration(string text)
        {
            return Regex.IsMatch(text, @"^(var|val|final\s+\w+|[A-Za-z_][\w.<>,\[\]\s]*)\s+[A-Za-z_]\w*\s*(=|;)");
        }

        private int FindSimpleEnd(int start, int limit)
        {
            string masked = _sc.Masked;
            int par = 0, brk = 0, brc = 0;

            for (int i = start; i < limit; i++)
            {
                char c = masked[i];
                switch (c)
                {
                    case '(': par++; break;
                    case ')': if (par > 0) par--; break;
                    case '[': brk++; break;
                    case ']': if (brk > 0) brk--; break;
                    case '{': brc++; break;
                    case '}':
                        if (brc == 0) return i;   // ket thuc block cha
                        brc--;
                        break;
                    case ';':
                        if (par == 0 && brk == 0 && brc == 0) return i + 1;
                        break;
                }
            }
            return limit;
        }

        private Stmt ParseIf(ref int pos, int wordEnd, int limit)
        {
            string masked = _sc.Masked;
            int start = pos;
            int p = _sc.SkipTrivia(wordEnd, limit);

            string condition = string.Empty;
            if (p < limit && masked[p] == '(')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    condition = _sc.Slice(p + 1, close);
                    p = close + 1;
                }
            }

            var stmt = NewStmt(StmtKind.If, start, p);
            stmt.Condition = SourceScanner.Collapse(condition);
            stmt.HeaderText = SourceScanner.Collapse(_sc.Slice(start, p));

            int q = p;
            var thenStmt = ParseOne(ref q, limit);
            stmt.Body = Flatten(thenStmt);

            int r = _sc.SkipTrivia(q, limit);
            string next = _sc.PeekWord(r, out int nextEnd);
            if (next == "else")
            {
                int s = nextEnd;
                var elseStmt = ParseOne(ref s, limit);
                stmt.Else = Flatten(elseStmt);
                q = s;
            }

            stmt.End = q;
            stmt.Text = _sc.Slice(start, q).Trim();
            pos = q;
            return stmt;
        }

        private Stmt ParseWhile(ref int pos, int wordEnd, int limit)
        {
            string masked = _sc.Masked;
            int start = pos;
            int p = _sc.SkipTrivia(wordEnd, limit);

            string condition = string.Empty;
            if (p < limit && masked[p] == '(')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    condition = _sc.Slice(p + 1, close);
                    p = close + 1;
                }
            }

            var stmt = NewStmt(StmtKind.While, start, p);
            stmt.Condition = SourceScanner.Collapse(condition);
            stmt.HeaderText = SourceScanner.Collapse(_sc.Slice(start, p));

            int q = p;
            var body = ParseOne(ref q, limit);
            stmt.Body = Flatten(body);

            stmt.End = q;
            stmt.Text = _sc.Slice(start, q).Trim();
            pos = q;
            return stmt;
        }

        private Stmt ParseFor(ref int pos, int wordEnd, int limit, bool isForEachKeyword)
        {
            string masked = _sc.Masked;
            int start = pos;
            int p = _sc.SkipTrivia(wordEnd, limit);

            string header = string.Empty;
            int headerStart = p + 1;
            int headerEnd = p;
            if (p < limit && masked[p] == '(')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    header = _sc.Slice(p + 1, close);
                    headerEnd = close;
                    p = close + 1;
                }
            }

            var stmt = NewStmt(StmtKind.For, start, p);
            stmt.HeaderText = SourceScanner.Collapse(_sc.Slice(start, p));

            int firstSemi = headerEnd > headerStart ? _sc.IndexOfAtDepthZero(headerStart, headerEnd, ';') : -1;
            if (isForEachKeyword || firstSemi < 0)
            {
                // foreach (var x in list) / for (String s : list)
                stmt.Kind = StmtKind.ForEach;
                stmt.Condition = SourceScanner.Collapse(header);
            }
            else
            {
                int secondSemi = _sc.IndexOfAtDepthZero(firstSemi + 1, headerEnd, ';');
                if (secondSemi < 0) secondSemi = headerEnd;

                stmt.ForInit = SourceScanner.Collapse(_sc.Slice(headerStart, firstSemi));
                stmt.Condition = SourceScanner.Collapse(_sc.Slice(firstSemi + 1, secondSemi));
                stmt.ForUpdate = SourceScanner.Collapse(_sc.Slice(Math.Min(secondSemi + 1, headerEnd), headerEnd));
            }

            int q = p;
            var body = ParseOne(ref q, limit);
            stmt.Body = Flatten(body);

            stmt.End = q;
            stmt.Text = _sc.Slice(start, q).Trim();
            pos = q;
            return stmt;
        }

        private Stmt ParseDoWhile(ref int pos, int wordEnd, int limit)
        {
            string masked = _sc.Masked;
            int start = pos;

            var stmt = NewStmt(StmtKind.DoWhile, start, wordEnd);

            int q = wordEnd;
            var body = ParseOne(ref q, limit);
            stmt.Body = Flatten(body);

            int r = _sc.SkipTrivia(q, limit);
            string next = _sc.PeekWord(r, out int nextEnd);
            if (next == "while")
            {
                int p = _sc.SkipTrivia(nextEnd, limit);
                if (p < limit && masked[p] == '(')
                {
                    int close = _sc.FindMatching(p, limit);
                    if (close > 0)
                    {
                        stmt.Condition = SourceScanner.Collapse(_sc.Slice(p + 1, close));
                        p = close + 1;
                    }
                }
                p = _sc.SkipTrivia(p, limit);
                if (p < limit && masked[p] == ';') p++;
                q = p;
            }

            stmt.End = q;
            stmt.HeaderText = "do ... while (" + stmt.Condition + ")";
            stmt.Text = _sc.Slice(start, q).Trim();
            pos = q;
            return stmt;
        }

        private Stmt ParseScoped(ref int pos, int wordEnd, int limit)
        {
            string masked = _sc.Masked;
            int start = pos;
            int p = _sc.SkipTrivia(wordEnd, limit);

            string header = string.Empty;
            if (p < limit && masked[p] == '(')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    header = _sc.Slice(p + 1, close);
                    p = close + 1;
                }
            }

            var stmt = NewStmt(StmtKind.Scoped, start, p);
            stmt.Condition = SourceScanner.Collapse(header);
            stmt.HeaderText = SourceScanner.Collapse(_sc.Slice(start, p));

            int q = p;
            var body = ParseOne(ref q, limit);
            stmt.Body = Flatten(body);

            stmt.End = q;
            stmt.Text = _sc.Slice(start, q).Trim();
            pos = q;
            return stmt;
        }

        private Stmt ParseSwitch(ref int pos, int wordEnd, int limit)
        {
            string masked = _sc.Masked;
            int start = pos;
            int p = _sc.SkipTrivia(wordEnd, limit);

            string selector = string.Empty;
            if (p < limit && masked[p] == '(')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    selector = _sc.Slice(p + 1, close);
                    p = close + 1;
                }
            }

            var stmt = NewStmt(StmtKind.Switch, start, p);
            stmt.Condition = SourceScanner.Collapse(selector);
            stmt.HeaderText = SourceScanner.Collapse(_sc.Slice(start, p));

            p = _sc.SkipTrivia(p, limit);
            if (p < limit && masked[p] == '{')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    ParseSwitchBody(p + 1, close, stmt);
                    p = close + 1;
                }
            }

            stmt.End = p;
            stmt.Text = _sc.Slice(start, p).Trim();
            pos = p;
            return stmt;
        }

        private void ParseSwitchBody(int start, int end, Stmt switchStmt)
        {
            string masked = _sc.Masked;
            var labels = new List<(int Start, int BodyStart, string Label, bool IsDefault)>();

            int i = start;
            int depth = 0;
            while (i < end)
            {
                if (++_guard > MaxIterations) break;

                char c = masked[i];
                if (c == '{' || c == '(' || c == '[') { depth++; i++; continue; }
                if (c == '}' || c == ')' || c == ']') { depth--; i++; continue; }

                if (depth == 0 && SourceScanner.IsIdentStart(c) && _sc.IsWordBoundary(i))
                {
                    string w = _sc.PeekWord(i, out int we);
                    if (w == "case" || w == "default")
                    {
                        int colon = _sc.IndexOfAtDepthZero(we, end, ':');
                        if (colon > 0)
                        {
                            string label = w == "default"
                                ? "default"
                                : SourceScanner.Collapse(_sc.Slice(we, colon));
                            labels.Add((i, colon + 1, label, w == "default"));
                            i = colon + 1;
                            continue;
                        }
                    }
                    i = we;
                    continue;
                }

                i++;
            }

            for (int k = 0; k < labels.Count; k++)
            {
                int bodyStart = labels[k].BodyStart;
                int bodyEnd = k + 1 < labels.Count ? labels[k + 1].Start : end;

                switchStmt.Cases.Add(new SwitchCaseInfo
                {
                    Label = labels[k].Label,
                    IsDefault = labels[k].IsDefault,
                    Line = _sc.LineOf(labels[k].Start),
                    Body = ParseStatements(bodyStart, bodyEnd)
                });
            }
        }

        private Stmt ParseTry(ref int pos, int wordEnd, int limit)
        {
            string masked = _sc.Masked;
            int start = pos;
            int p = _sc.SkipTrivia(wordEnd, limit);

            var stmt = NewStmt(StmtKind.Try, start, p);
            stmt.HeaderText = "try";

            // try-with-resources (Java) / using (...) khong bat buoc
            if (p < limit && masked[p] == '(')
            {
                int closeParen = _sc.FindMatching(p, limit);
                if (closeParen > 0)
                {
                    stmt.Condition = SourceScanner.Collapse(_sc.Slice(p + 1, closeParen));
                    p = _sc.SkipTrivia(closeParen + 1, limit);
                }
            }

            if (p < limit && masked[p] == '{')
            {
                int close = _sc.FindMatching(p, limit);
                if (close > 0)
                {
                    stmt.Body = ParseStatements(p + 1, close);
                    p = close + 1;
                }
            }

            while (true)
            {
                if (++_guard > MaxIterations) break;

                int r = _sc.SkipTrivia(p, limit);
                string w = _sc.PeekWord(r, out int we);

                if (w == "catch")
                {
                    var clause = new CatchClauseInfo { Line = _sc.LineOf(r), Start = r };
                    int q = _sc.SkipTrivia(we, limit);

                    if (q < limit && masked[q] == '(')
                    {
                        int closeParen = _sc.FindMatching(q, limit);
                        if (closeParen > 0)
                        {
                            clause.Parameter = SourceScanner.Collapse(_sc.Slice(q + 1, closeParen));
                            clause.ExceptionType = ExtractExceptionType(clause.Parameter);
                            q = _sc.SkipTrivia(closeParen + 1, limit);
                        }
                    }

                    // C# exception filter: catch (Ex e) when (...)
                    string filterWord = _sc.PeekWord(q, out int filterEnd);
                    if (filterWord == "when")
                    {
                        int fq = _sc.SkipTrivia(filterEnd, limit);
                        if (fq < limit && masked[fq] == '(')
                        {
                            int closeFilter = _sc.FindMatching(fq, limit);
                            if (closeFilter > 0) q = _sc.SkipTrivia(closeFilter + 1, limit);
                        }
                    }

                    if (q < limit && masked[q] == '{')
                    {
                        int close = _sc.FindMatching(q, limit);
                        if (close > 0)
                        {
                            clause.Body = ParseStatements(q + 1, close);
                            q = close + 1;
                        }
                    }

                    clause.HeaderText = string.IsNullOrEmpty(clause.Parameter)
                        ? "catch"
                        : "catch (" + clause.Parameter + ")";
                    stmt.Catches.Add(clause);
                    p = q;
                    continue;
                }

                if (w == "finally")
                {
                    int q = _sc.SkipTrivia(we, limit);
                    if (q < limit && masked[q] == '{')
                    {
                        int close = _sc.FindMatching(q, limit);
                        if (close > 0)
                        {
                            stmt.Finally = ParseStatements(q + 1, close);
                            q = close + 1;
                        }
                    }
                    p = q;
                    continue;
                }

                break;
            }

            stmt.End = p;
            stmt.Text = _sc.Slice(start, p).Trim();
            pos = p;
            return stmt;
        }

        private static string ExtractExceptionType(string parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter)) return "Exception";
            var match = Regex.Match(parameter, @"([A-Za-z_][\w.]*)");
            return match.Success ? match.Groups[1].Value : "Exception";
        }

        private static List<Stmt> Flatten(Stmt? stmt)
        {
            if (stmt == null) return new List<Stmt>();
            if (stmt.Kind == StmtKind.Block) return stmt.Body;
            return new List<Stmt> { stmt };
        }
    }
}
