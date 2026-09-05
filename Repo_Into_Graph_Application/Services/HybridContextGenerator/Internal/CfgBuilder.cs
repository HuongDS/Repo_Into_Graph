using System;
using System.Collections.Generic;
using System.Linq;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Dung Control Flow Graph tu cay cau lenh.
    /// Moi ham duoc dung thanh mot do thi con: Start -> ... -> End.
    /// Node duoc danh id kieu A, B, C... de so do Mermaid de doc.
    /// </summary>
    internal sealed class CfgBuilder
    {
        private const int MaxNodes = 600;

        private readonly List<CfgNodeDto> _nodes = new();
        private readonly List<CfgEdgeDto> _edges = new();
        private readonly HashSet<string> _edgeKeys = new(StringComparer.Ordinal);

        private readonly List<Stub> _returnStubs = new();
        private readonly List<Stub> _abnormalStubs = new();
        private readonly Stack<BreakScope> _breakScopes = new();
        private readonly Stack<LoopScope> _loopScopes = new();
        private readonly Stack<TryScope> _tryScopes = new();

        private int _seq;
        private int _depth;
        private string? _truncatedNodeId;
        private string _currentMethod = string.Empty;

        /// <summary>Anh xa vi tri cau lenh (Stmt.Start) -> id node CFG, de gan Critical Snippet vao node.</summary>
        public Dictionary<int, string> StmtNodeMap { get; } = new();

        public IReadOnlyList<CfgNodeDto> Nodes => _nodes;
        public IReadOnlyList<CfgEdgeDto> Edges => _edges;
        public ControlFlowStatsDto Stats { get; } = new();
        public bool Truncated => _truncatedNodeId != null;

        private readonly struct Stub
        {
            public Stub(string from, string label) { From = from; Label = label; }
            public string From { get; }
            public string Label { get; }
        }

        private sealed class BreakScope
        {
            public List<Stub> Exits { get; } = new();
        }

        private sealed class LoopScope
        {
            public LoopScope(string continueTarget) { ContinueTarget = continueTarget; }
            public string ContinueTarget { get; }
        }

        private sealed class TryScope
        {
            public List<(string NodeId, string ExceptionType)> Catches { get; } = new();
        }

        public void BuildMethod(MethodDecl method)
        {
            _currentMethod = string.IsNullOrEmpty(method.Name) ? "main" : method.Name;

            _returnStubs.Clear();
            _abnormalStubs.Clear();
            _breakScopes.Clear();
            _loopScopes.Clear();
            _tryScopes.Clear();

            string startId = AddNode("Start", "START", method.StartLine, method.Signature);
            var outs = BuildBlock(method.Body, Single(startId, string.Empty));

            string endId = AddNode("End", "END", 0, string.Empty);
            Connect(outs, endId);
            Connect(_returnStubs, endId);
            foreach (var stub in _abnormalStubs)
            {
                AddEdge(stub.From, endId, string.IsNullOrEmpty(stub.Label) ? "throws" : stub.Label);
            }
        }

        private List<Stub> BuildBlock(List<Stmt> statements, List<Stub> incoming)
        {
            var current = incoming;
            foreach (var stmt in statements)
            {
                current = BuildStatement(stmt, current);
            }
            return current;
        }

        private List<Stub> BuildStatement(Stmt stmt, List<Stub> incoming)
        {
            int nodeIndexBefore = _nodes.Count;
            var result = BuildStatementCore(stmt, incoming);

            // Node dau tien do cau lenh nay tao ra chinh la node dai dien cua no
            if (_nodes.Count > nodeIndexBefore && !StmtNodeMap.ContainsKey(stmt.Start))
            {
                StmtNodeMap[stmt.Start] = _nodes[nodeIndexBefore].Id;
            }
            return result;
        }

        private List<Stub> BuildStatementCore(Stmt stmt, List<Stub> incoming)
        {
            switch (stmt.Kind)
            {
                case StmtKind.Block:
                    return BuildBlock(stmt.Body, incoming);

                case StmtKind.If:
                    return BuildIf(stmt, incoming);

                case StmtKind.While:
                    return BuildWhile(stmt, incoming);

                case StmtKind.For:
                    return BuildFor(stmt, incoming);

                case StmtKind.ForEach:
                    return BuildForEach(stmt, incoming);

                case StmtKind.DoWhile:
                    return BuildDoWhile(stmt, incoming);

                case StmtKind.Switch:
                    return BuildSwitch(stmt, incoming);

                case StmtKind.Try:
                    return BuildTry(stmt, incoming);

                case StmtKind.Scoped:
                    {
                        string id = AddNode(NodeLabelHumanizer.Truncate(stmt.HeaderText), "PROCESS", stmt.Line, stmt.HeaderText);
                        Connect(incoming, id);
                        EnterDepth();
                        var outs = BuildBlock(stmt.Body, Single(id, string.Empty));
                        ExitDepth();
                        return outs;
                    }

                case StmtKind.Return:
                    {
                        Stats.ReturnCount++;
                        string id = AddNode(NodeLabelHumanizer.Return(stmt.Text), "RETURN", stmt.Line, stmt.Text);
                        Connect(incoming, id);
                        _returnStubs.Add(new Stub(id, string.Empty));
                        return new List<Stub>();
                    }

                case StmtKind.Throw:
                    {
                        Stats.ThrowCount++;
                        string id = AddNode(NodeLabelHumanizer.Throw(stmt.Text), "THROW", stmt.Line, stmt.Text);
                        Connect(incoming, id);
                        RegisterThrow(id, stmt.Text);
                        return new List<Stub>();
                    }

                case StmtKind.Break:
                    {
                        if (_breakScopes.Count > 0) _breakScopes.Peek().Exits.AddRange(incoming);
                        return new List<Stub>();
                    }

                case StmtKind.Continue:
                    {
                        if (_loopScopes.Count > 0)
                        {
                            string target = _loopScopes.Peek().ContinueTarget;
                            foreach (var stub in incoming)
                            {
                                // Giu nhan nhanh (Yes/No) neu co, de so do van doc duoc dieu kien re
                                AddEdge(stub.From, target, string.IsNullOrEmpty(stub.Label) ? "continue" : stub.Label);
                            }
                        }
                        return new List<Stub>();
                    }

                default:
                    {
                        string id = AddNode(NodeLabelHumanizer.Process(stmt.Text), "PROCESS", stmt.Line, stmt.Text);
                        Connect(incoming, id);
                        return Single(id, string.Empty);
                    }
            }
        }

        private List<Stub> BuildIf(Stmt stmt, List<Stub> incoming)
        {
            Stats.BranchCount++;
            string decision = AddNode(NodeLabelHumanizer.Decision(stmt.Condition), "DECISION", stmt.Line, stmt.HeaderText);
            Connect(incoming, decision);

            EnterDepth();
            var thenOuts = BuildBlock(stmt.Body, Single(decision, "Yes"));
            var elseOuts = stmt.Else.Count > 0
                ? BuildBlock(stmt.Else, Single(decision, "No"))
                : Single(decision, "No");
            ExitDepth();

            var result = new List<Stub>(thenOuts);
            result.AddRange(elseOuts);
            return result;
        }

        private List<Stub> BuildWhile(Stmt stmt, List<Stub> incoming)
        {
            Stats.LoopCount++;
            string decision = AddNode(NodeLabelHumanizer.Loop(StmtKind.While, stmt), "DECISION", stmt.Line, stmt.HeaderText);
            Connect(incoming, decision);

            var breakScope = new BreakScope();
            _breakScopes.Push(breakScope);
            _loopScopes.Push(new LoopScope(decision));
            EnterDepth();

            var bodyOuts = BuildBlock(stmt.Body, Single(decision, "Yes"));
            Connect(bodyOuts, decision, "loop");

            ExitDepth();
            _loopScopes.Pop();
            _breakScopes.Pop();

            var result = Single(decision, "No");
            result.AddRange(breakScope.Exits);
            return result;
        }

        private List<Stub> BuildFor(Stmt stmt, List<Stub> incoming)
        {
            Stats.LoopCount++;
            var entry = incoming;

            if (!string.IsNullOrWhiteSpace(stmt.ForInit))
            {
                string initId = AddNode("Init " + NodeLabelHumanizer.Truncate(stmt.ForInit), "PROCESS", stmt.Line, stmt.ForInit);
                Connect(entry, initId);
                entry = Single(initId, string.Empty);
            }

            string decision = AddNode(NodeLabelHumanizer.Loop(StmtKind.For, stmt), "DECISION", stmt.Line, stmt.HeaderText);
            Connect(entry, decision);

            string continueTarget = decision;
            string? updateId = null;
            if (!string.IsNullOrWhiteSpace(stmt.ForUpdate))
            {
                updateId = AddNode("Next " + NodeLabelHumanizer.Truncate(stmt.ForUpdate), "PROCESS", stmt.Line, stmt.ForUpdate);
                continueTarget = updateId;
            }

            var breakScope = new BreakScope();
            _breakScopes.Push(breakScope);
            _loopScopes.Push(new LoopScope(continueTarget));
            EnterDepth();

            var bodyOuts = BuildBlock(stmt.Body, Single(decision, "Yes"));

            if (updateId != null)
            {
                Connect(bodyOuts, updateId);
                AddEdge(updateId, decision, "loop");
            }
            else
            {
                Connect(bodyOuts, decision, "loop");
            }

            ExitDepth();
            _loopScopes.Pop();
            _breakScopes.Pop();

            var result = Single(decision, "No");
            result.AddRange(breakScope.Exits);
            return result;
        }

        private List<Stub> BuildForEach(Stmt stmt, List<Stub> incoming)
        {
            Stats.LoopCount++;
            string decision = AddNode(NodeLabelHumanizer.Loop(StmtKind.ForEach, stmt), "DECISION", stmt.Line, stmt.HeaderText);
            Connect(incoming, decision);

            var breakScope = new BreakScope();
            _breakScopes.Push(breakScope);
            _loopScopes.Push(new LoopScope(decision));
            EnterDepth();

            var bodyOuts = BuildBlock(stmt.Body, Single(decision, "Yes"));
            Connect(bodyOuts, decision, "loop");

            ExitDepth();
            _loopScopes.Pop();
            _breakScopes.Pop();

            var result = Single(decision, "No");
            result.AddRange(breakScope.Exits);
            return result;
        }

        private List<Stub> BuildDoWhile(Stmt stmt, List<Stub> incoming)
        {
            Stats.LoopCount++;
            string entryId = AddNode("Do", "PROCESS", stmt.Line, "do");
            Connect(incoming, entryId);

            string decision = AddNode(NodeLabelHumanizer.Loop(StmtKind.DoWhile, stmt), "DECISION", stmt.Line, stmt.HeaderText);

            var breakScope = new BreakScope();
            _breakScopes.Push(breakScope);
            _loopScopes.Push(new LoopScope(decision));
            EnterDepth();

            var bodyOuts = BuildBlock(stmt.Body, Single(entryId, string.Empty));
            Connect(bodyOuts, decision);

            ExitDepth();
            _loopScopes.Pop();
            _breakScopes.Pop();

            AddEdge(decision, entryId, "Yes");

            var result = Single(decision, "No");
            result.AddRange(breakScope.Exits);
            return result;
        }

        private List<Stub> BuildSwitch(Stmt stmt, List<Stub> incoming)
        {
            Stats.SwitchCount++;
            string decision = AddNode(NodeLabelHumanizer.Switch(stmt.Condition), "DECISION", stmt.Line, stmt.HeaderText);
            Connect(incoming, decision);

            var breakScope = new BreakScope();
            _breakScopes.Push(breakScope);
            EnterDepth();

            var outs = new List<Stub>();
            bool hasDefault = false;

            foreach (var branch in stmt.Cases)
            {
                if (branch.IsDefault) hasDefault = true;
                string edgeLabel = branch.IsDefault ? "default" : "case " + NodeLabelHumanizer.Truncate(branch.Label, 24);
                var caseOuts = BuildBlock(branch.Body, Single(decision, edgeLabel));
                outs.AddRange(caseOuts);
            }

            ExitDepth();
            _breakScopes.Pop();

            if (!hasDefault) outs.Add(new Stub(decision, "default"));
            outs.AddRange(breakScope.Exits);
            return outs;
        }

        private List<Stub> BuildTry(Stmt stmt, List<Stub> incoming)
        {
            Stats.TryCatchCount++;
            string tryId = AddNode("Try", "PROCESS", stmt.Line,
                string.IsNullOrEmpty(stmt.Condition) ? "try" : "try (" + stmt.Condition + ")");
            Connect(incoming, tryId);

            // Tao truoc cac node catch de cac lenh throw ben trong co the tro toi
            var scope = new TryScope();
            foreach (var clause in stmt.Catches)
            {
                string catchId = AddNode(NodeLabelHumanizer.Catch(clause.ExceptionType), "CATCH", clause.Line, clause.HeaderText);
                if (clause.Start > 0) StmtNodeMap[clause.Start] = catchId;
                scope.Catches.Add((catchId, clause.ExceptionType));
            }

            _tryScopes.Push(scope);
            EnterDepth();
            var bodyOuts = BuildBlock(stmt.Body, Single(tryId, string.Empty));
            ExitDepth();
            _tryScopes.Pop();

            // Canh "co exception" tu khoi try sang tung handler
            foreach (var (catchId, _) in scope.Catches)
            {
                AddEdge(tryId, catchId, "exception");
            }

            var allOuts = new List<Stub>(bodyOuts);
            for (int i = 0; i < stmt.Catches.Count && i < scope.Catches.Count; i++)
            {
                EnterDepth();
                var catchOuts = BuildBlock(stmt.Catches[i].Body, Single(scope.Catches[i].NodeId, string.Empty));
                ExitDepth();
                allOuts.AddRange(catchOuts);
            }

            if (stmt.Finally.Count > 0)
            {
                string finallyId = AddNode("Finally", "FINALLY", stmt.Line, "finally");
                Connect(allOuts, finallyId);
                EnterDepth();
                var finallyOuts = BuildBlock(stmt.Finally, Single(finallyId, string.Empty));
                ExitDepth();
                allOuts = finallyOuts;
            }

            return allOuts;
        }

        private void RegisterThrow(string nodeId, string text)
        {
            if (_tryScopes.Count > 0)
            {
                var scope = _tryScopes.Peek();
                if (scope.Catches.Count > 0)
                {
                    string thrownType = NodeLabelHumanizer.ExtractThrownType(text);
                    var match = scope.Catches.FirstOrDefault(c =>
                        !string.IsNullOrEmpty(c.ExceptionType) &&
                        string.Equals(c.ExceptionType, thrownType, StringComparison.OrdinalIgnoreCase));

                    string target = string.IsNullOrEmpty(match.NodeId) ? scope.Catches[0].NodeId : match.NodeId;
                    AddEdge(nodeId, target, "exception");
                    return;
                }
            }
            _abnormalStubs.Add(new Stub(nodeId, "throws"));
        }

        private string AddNode(string label, string kind, int line, string code)
        {
            if (_nodes.Count >= MaxNodes)
            {
                if (_truncatedNodeId == null)
                {
                    _truncatedNodeId = ToAlphaId(_seq++);
                    _nodes.Add(new CfgNodeDto
                    {
                        Id = _truncatedNodeId,
                        Label = "... CFG rut gon ...",
                        Kind = "PROCESS",
                        Method = _currentMethod
                    });
                }
                return _truncatedNodeId;
            }

            string id = ToAlphaId(_seq++);
            _nodes.Add(new CfgNodeDto
            {
                Id = id,
                Label = string.IsNullOrWhiteSpace(label) ? "(empty)" : label,
                Kind = kind,
                Line = line,
                Code = SourceScanner.Collapse(code),
                Method = _currentMethod
            });
            return id;
        }

        /// <summary>Sinh id kieu Excel: A, B, ... Z, AA, AB...</summary>
        internal static string ToAlphaId(int index)
        {
            var sb = new System.Text.StringBuilder();
            int value = index + 1;
            while (value > 0)
            {
                value--;
                sb.Insert(0, (char)('A' + (value % 26)));
                value /= 26;
            }
            return sb.ToString();
        }

        private void AddEdge(string from, string to, string label = "")
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (from == to && string.IsNullOrEmpty(label)) return;

            string key = from + "->" + to + "#" + label;
            if (!_edgeKeys.Add(key)) return;

            _edges.Add(new CfgEdgeDto { From = from, To = to, Label = label ?? string.Empty });
        }

        private void Connect(List<Stub> stubs, string target, string overrideLabel = "")
        {
            foreach (var stub in stubs)
            {
                string label = string.IsNullOrEmpty(stub.Label) ? overrideLabel : stub.Label;
                AddEdge(stub.From, target, label);
            }
        }

        private static List<Stub> Single(string nodeId, string label)
        {
            return new List<Stub> { new Stub(nodeId, label) };
        }

        private void EnterDepth()
        {
            _depth++;
            if (_depth > Stats.MaxNestingDepth) Stats.MaxNestingDepth = _depth;
        }

        private void ExitDepth()
        {
            if (_depth > 0) _depth--;
        }
    }
}
