using System.Collections.Generic;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    internal enum StmtKind
    {
        Simple,
        Declaration,
        Return,
        Throw,
        Break,
        Continue,
        If,
        While,
        For,
        ForEach,
        DoWhile,
        Switch,
        Try,
        Block,
        Scoped   // using (...) / lock (...) / synchronized (...)
    }

    /// <summary>Mot cau lenh trong than ham (cay cau truc rut gon).</summary>
    internal sealed class Stmt
    {
        public StmtKind Kind { get; set; } = StmtKind.Simple;

        /// <summary>Vi tri bat dau / ket thuc (exclusive) trong raw source.</summary>
        public int Start { get; set; }
        public int End { get; set; }

        /// <summary>Dong bat dau (1-based).</summary>
        public int Line { get; set; }

        /// <summary>Toan van cau lenh (da trim).</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Phan dau: "if (x > 0)", "for (...)", "using (...)"...</summary>
        public string HeaderText { get; set; } = string.Empty;

        /// <summary>Bieu thuc dieu kien (noi dung trong ngoac).</summary>
        public string Condition { get; set; } = string.Empty;

        public string ForInit { get; set; } = string.Empty;
        public string ForUpdate { get; set; } = string.Empty;

        /// <summary>Than cau lenh (then-branch, than vong lap, than block...).</summary>
        public List<Stmt> Body { get; set; } = new();

        /// <summary>Nhanh else.</summary>
        public List<Stmt> Else { get; set; } = new();

        public List<CatchClauseInfo> Catches { get; set; } = new();
        public List<Stmt> Finally { get; set; } = new();
        public List<SwitchCaseInfo> Cases { get; set; } = new();
    }

    internal sealed class CatchClauseInfo
    {
        /// <summary>Vi tri tu khoa "catch" trong raw source (de anh xa sang node CFG).</summary>
        public int Start { get; set; }

        public string Parameter { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;
        public int Line { get; set; }
        public List<Stmt> Body { get; set; } = new();
    }

    internal sealed class SwitchCaseInfo
    {
        public string Label { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public int Line { get; set; }
        public List<Stmt> Body { get; set; } = new();
    }

    /// <summary>Khai bao mot ham/phuong thuc tim thay trong ma nguon.</summary>
    internal sealed class MethodDecl
    {
        public string Name { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public List<string> Modifiers { get; set; } = new();
        public List<string> Annotations { get; set; } = new();
        public List<string> ThrowsTypes { get; set; } = new();

        public int StartLine { get; set; }
        public int BodyStart { get; set; }
        public int BodyEnd { get; set; }

        public bool IsAsync { get; set; }
        public bool IsStatic { get; set; }

        /// <summary>Ham gia lap (khi ma nguon chi la mot doan lenh, khong co khai bao ham).</summary>
        public bool IsSynthetic { get; set; }

        public List<Stmt> Body { get; set; } = new();
    }
}
