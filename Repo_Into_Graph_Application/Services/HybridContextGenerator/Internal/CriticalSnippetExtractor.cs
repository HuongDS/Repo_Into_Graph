using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Trich xuat Critical Snippets: cac doan code ma LLM BAT BUOC phai nhin thay nguyen van
    /// (nhanh if, throw, catch, vong lap phuc tap, switch, loi goi async / phu thuoc ngoai).
    /// Phan con lai da duoc CFG skeleton tom tat nen khong can dua vao prompt.
    /// </summary>
    internal sealed class CriticalSnippetExtractor
    {
        private const int MaxSnippets = 40;
        private const int MaxSnippetLength = 400;

        private static readonly Regex DependencyCallPattern =
            new(@"^(await\s+)?[A-Za-z_]\w*(\s*\.\s*[A-Za-z_]\w*)+\s*\(", RegexOptions.Compiled);

        private static readonly Regex AsyncPattern =
            new(@"\bawait\b|\b\w*Async\s*\(|\bCompletableFuture\b|\bTask\.Run\b|\.subscribe\s*\(|\bExecutorService\b",
                RegexOptions.Compiled);

        private readonly List<CriticalSnippetDto> _snippets = new();
        private Dictionary<int, string> _nodeMap = new();
        private string _currentMethod = string.Empty;

        public bool HasComplexLoop { get; private set; }

        /// <param name="methods">Cac ham da duoc phan tich.</param>
        /// <param name="nodeMap">Anh xa Stmt.Start -> id node CFG (de gan snippet vao dung node).</param>
        public List<CriticalSnippetDto> Extract(List<MethodDecl> methods, Dictionary<int, string>? nodeMap = null)
        {
            _snippets.Clear();
            _nodeMap = nodeMap ?? new Dictionary<int, string>();

            foreach (var method in methods)
            {
                _currentMethod = method.Name;
                Walk(method.Body, 0);
            }

            // Uu tien theo trong so, sau do sap xep lai theo thu tu xuat hien trong file
            var selected = _snippets
                .GroupBy(s => s.Line + "|" + s.Code, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(s => s.Weight).First())
                .OrderByDescending(s => s.Weight)
                .Take(MaxSnippets)
                .OrderBy(s => s.Line)
                .ToList();

            return RemoveNestedDuplicates(selected);
        }

        private void Walk(List<Stmt> statements, int depth)
        {
            foreach (var stmt in statements)
            {
                WalkStatement(stmt, depth);
            }
        }

        private void WalkStatement(Stmt stmt, int depth)
        {
            switch (stmt.Kind)
            {
                case StmtKind.If:
                    Add(stmt, "BRANCH", "Nhanh dieu kien quyet dinh luong xu ly", 9);
                    Walk(stmt.Body, depth + 1);
                    Walk(stmt.Else, depth + 1);
                    break;

                case StmtKind.Throw:
                    Add(stmt, "THROW", "Diem nem ngoai le - can sinh test case cho truong hop loi", 10);
                    break;

                case StmtKind.Switch:
                    Add(stmt, "SWITCH", "Re nhanh nhieu truong hop", 8);
                    foreach (var branch in stmt.Cases) Walk(branch.Body, depth + 1);
                    break;

                case StmtKind.While:
                case StmtKind.For:
                case StmtKind.ForEach:
                case StmtKind.DoWhile:
                    {
                        bool complex = IsComplexLoop(stmt);
                        if (complex) HasComplexLoop = true;
                        Add(stmt, "LOOP",
                            complex
                                ? "Vong lap phuc tap (co re nhanh / lap long ben trong)"
                                : "Vong lap - can kiem thu bien 0/1/n phan tu",
                            complex ? 9 : 7);
                        Walk(stmt.Body, depth + 1);
                        break;
                    }

                case StmtKind.Try:
                    foreach (var clause in stmt.Catches)
                    {
                        _snippets.Add(new CriticalSnippetDto
                        {
                            Code = Normalize(clause.HeaderText),
                            NodeId = LookupNode(clause.Start),
                            Kind = "CATCH",
                            Reason = "Khoi bat ngoai le - xac dinh hanh vi khi loi xay ra",
                            Line = clause.Line,
                            Weight = 9,
                            Method = _currentMethod
                        });
                        Walk(clause.Body, depth + 1);
                    }
                    Walk(stmt.Body, depth + 1);
                    Walk(stmt.Finally, depth + 1);
                    break;

                case StmtKind.Block:
                case StmtKind.Scoped:
                    Walk(stmt.Body, depth + 1);
                    break;

                case StmtKind.Return:
                    if (depth > 0) Add(stmt, "RETURN", "Diem thoat som ben trong nhanh dieu kien", 6);
                    break;

                default:
                    {
                        string text = stmt.Text ?? string.Empty;
                        if (AsyncPattern.IsMatch(text))
                        {
                            Add(stmt, "ASYNC", "Loi goi bat dong bo - anh huong thu tu thuc thi", 8);
                        }
                        else if (DependencyCallPattern.IsMatch(text.TrimStart()))
                        {
                            Add(stmt, "DEPENDENCY_CALL", "Loi goi sang thanh phan phu thuoc ben ngoai", 5);
                        }
                        break;
                    }
            }
        }

        private string LookupNode(int start)
        {
            return _nodeMap.TryGetValue(start, out var id) ? id : string.Empty;
        }

        /// <summary>
        /// Bo cac snippet la chuoi con cua snippet khac.
        /// Vd: "throw new BadRequest();" da nam tron trong
        ///     "if (req == null) throw new BadRequest();" -> khong lap lai cho ton token.
        /// </summary>
        private static List<CriticalSnippetDto> RemoveNestedDuplicates(List<CriticalSnippetDto> snippets)
        {
            var result = new List<CriticalSnippetDto>();

            foreach (var snippet in snippets)
            {
                bool containedInAnother = snippets.Any(other =>
                    !ReferenceEquals(other, snippet) &&
                    other.Code.Length > snippet.Code.Length &&
                    other.Code.Contains(snippet.Code, StringComparison.Ordinal));

                if (!containedInAnother) result.Add(snippet);
            }

            return result;
        }

        private static bool IsComplexLoop(Stmt loop)
        {
            if (loop.Body.Count >= 4) return true;

            foreach (var child in loop.Body)
            {
                switch (child.Kind)
                {
                    case StmtKind.If:
                    case StmtKind.Switch:
                    case StmtKind.While:
                    case StmtKind.For:
                    case StmtKind.ForEach:
                    case StmtKind.DoWhile:
                    case StmtKind.Try:
                        return true;
                }
            }
            return false;
        }

        private void Add(Stmt stmt, string kind, string reason, int weight)
        {
            _snippets.Add(new CriticalSnippetDto
            {
                Code = SnippetText(stmt),
                NodeId = LookupNode(stmt.Start),
                Kind = kind,
                Reason = reason,
                Line = stmt.Line,
                Weight = weight,
                Method = _currentMethod
            });
        }

        /// <summary>
        /// Lay nguyen van cau lenh neu no nam gon tren mot dong;
        /// neu trai dai nhieu dong thi chi lay dong dau (phan header) de tiet kiem token.
        /// </summary>
        private static string SnippetText(Stmt stmt)
        {
            string text = (stmt.Text ?? string.Empty).Trim();
            if (text.Length == 0) return Normalize(stmt.HeaderText);

            if (!text.Contains('\n') && text.Length <= MaxSnippetLength) return text;

            string firstLine = text.Split('\n')[0].TrimEnd('\r').Trim();
            if (firstLine.Length == 0) firstLine = Normalize(stmt.HeaderText);
            return firstLine.Length > MaxSnippetLength ? firstLine.Substring(0, MaxSnippetLength) + "..." : firstLine;
        }

        private static string Normalize(string text)
        {
            string value = SourceScanner.Collapse(text);
            return value.Length > MaxSnippetLength ? value.Substring(0, MaxSnippetLength) + "..." : value;
        }
    }
}
