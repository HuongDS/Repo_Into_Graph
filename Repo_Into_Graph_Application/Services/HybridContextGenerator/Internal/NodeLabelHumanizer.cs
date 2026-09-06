using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Chuyen cau lenh thanh nhan mo ta ngan gon cho node CFG.
    /// Muc tieu: so do doc duoc nhu ngon ngu tu nhien ("Check count >= MAX", "Throw Overload",
    /// "Save req") thay vi day nguyen code vao - nho do tiet kiem token cho prompt Tang 3.
    /// Ma nguon nguyen ban van duoc giu trong CfgNodeDto.Code de truy vet.
    /// </summary>
    internal static class NodeLabelHumanizer
    {
        public const int MaxLabelLength = 70;

        private static readonly Regex CallPattern =
            new(@"^(?<await>await\s+)?(?<recv>[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*?\s*\.\s*)?(?<name>[A-Za-z_]\w*)\s*\((?<args>.*)\)$",
                RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex AssignPattern =
            new(@"^(?<lhs>[A-Za-z_][\w\.\[\]<>, ]*?)\s*(?<op>=|\+=|-=|\*=|/=|%=|\|=|&=)\s*(?<rhs>.+)$",
                RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex DeclarationPattern =
            new(@"^(?:final\s+|const\s+|readonly\s+)?(?<type>[A-Za-z_][\w\.<>,\[\]\s]*?)\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<rhs>.+)$",
                RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex StepPattern =
            new(@"^(?<name>[A-Za-z_][\w\.\[\]]*)\s*(?<op>\+\+|--)$|^(?<op2>\+\+|--)\s*(?<name2>[A-Za-z_][\w\.\[\]]*)$",
                RegexOptions.Compiled);

        public static string Truncate(string text, int max = MaxLabelLength)
        {
            string value = SourceScanner.Collapse(text);
            if (value.Length <= max) return value;
            return value.Substring(0, Math.Max(1, max - 3)) + "...";
        }

        /// <summary>
        /// Nhan cho node dieu kien theo quy uoc cua checklist ban giao:
        ///   req == null        -> "req == null?"
        ///   isConflict(req)    -> "isConflict?"      (bo tham so cho gon)
        ///   !svc.canBook(a, b) -> "!svc.canBook?"
        /// </summary>
        public static string Decision(string condition)
        {
            string value = SourceScanner.Collapse(condition);
            if (string.IsNullOrEmpty(value)) return "condition?";

            // Dieu kien chi la mot loi goi ham -> lay ten ham, bo danh sach tham so
            var call = Regex.Match(value, @"^(?<neg>!?\s*)(?<name>[A-Za-z_][\w.]*)\s*\([^()]*\)$");
            if (call.Success)
            {
                value = call.Groups["neg"].Value.Replace(" ", string.Empty) + call.Groups["name"].Value;
            }

            return EnsureQuestion(Truncate(value));
        }

        /// <summary>Nhan node quyet dinh luon ket thuc bang dau hoi.</summary>
        private static string EnsureQuestion(string label)
        {
            if (string.IsNullOrEmpty(label)) return "condition?";
            return label.EndsWith("?", StringComparison.Ordinal) ? label : label + "?";
        }

        /// <summary>Nhan cho vong lap: "Loop while i &lt; n", "For each item in items".</summary>
        public static string Loop(StmtKind kind, Stmt stmt)
        {
            string condition = SourceScanner.Collapse(stmt.Condition);

            switch (kind)
            {
                case StmtKind.ForEach:
                    {
                        var match = Regex.Match(condition, @"^(?<decl>.+?)\s*(?::|\bin\b)\s*(?<src>.+)$");
                        if (match.Success)
                        {
                            string variable = LastIdentifier(match.Groups["decl"].Value);
                            return EnsureQuestion(Truncate("For each " + variable + " in " + match.Groups["src"].Value));
                        }
                        return EnsureQuestion(Truncate("For each " + condition));
                    }

                case StmtKind.DoWhile:
                    return EnsureQuestion(Truncate("Repeat while " + (string.IsNullOrEmpty(condition) ? "true" : condition)));

                default:
                    return EnsureQuestion(Truncate("Loop while " + (string.IsNullOrEmpty(condition) ? "true" : condition)));
            }
        }

        /// <summary>Nhan cho switch: "Switch on order.getType()".</summary>
        public static string Switch(string selector)
        {
            string value = SourceScanner.Collapse(selector);
            return EnsureQuestion(Truncate(string.IsNullOrEmpty(value) ? "Switch" : "Switch on " + value));
        }

        /// <summary>Nhan cho throw: "Throw Overload" (bo hau to Exception/Error).</summary>
        public static string Throw(string text)
        {
            string type = ExtractThrownType(text);
            if (!string.IsNullOrEmpty(type)) return Truncate("Throw " + ShortenExceptionName(type));

            string value = SourceScanner.Collapse(text).TrimEnd(';').Trim();
            if (value.StartsWith("throw", StringComparison.Ordinal)) value = value.Substring(5).Trim();
            return Truncate(string.IsNullOrEmpty(value) ? "Throw" : "Throw " + value);
        }

        /// <summary>Nhan cho catch: "Catch Timeout".</summary>
        public static string Catch(string exceptionType)
        {
            return Truncate("Catch " + ShortenExceptionName(
                string.IsNullOrEmpty(exceptionType) ? "Exception" : exceptionType));
        }

        /// <summary>Nhan cho return: "Return total".</summary>
        public static string Return(string text)
        {
            string value = SourceScanner.Collapse(text).TrimEnd(';').Trim();
            if (value.StartsWith("return", StringComparison.Ordinal)) value = value.Substring(6).Trim();
            return string.IsNullOrEmpty(value) ? "Return" : Truncate("Return " + value);
        }

        /// <summary>
        /// Nhan cho cau lenh thuong:
        ///   save(req)                  -> "Save req"
        ///   emailService.sendAsync(u)  -> "Send Async u (emailService)"
        ///   double total = 0           -> "Set total = 0"
        /// </summary>
        public static string Process(string text)
        {
            string value = SourceScanner.Collapse(text).TrimEnd(';').Trim();
            if (string.IsNullOrEmpty(value)) return "(empty)";

            var call = CallPattern.Match(value);
            if (call.Success)
            {
                string name = call.Groups["name"].Value;
                string receiver = call.Groups["recv"].Success
                    ? call.Groups["recv"].Value.TrimEnd('.', ' ').Trim()
                    : string.Empty;

                if (!IsControlWord(name))
                {
                    string label = Humanize(name);
                    string args = SimpleArguments(call.Groups["args"].Value);
                    if (!string.IsNullOrEmpty(args)) label += " " + args;
                    if (!string.IsNullOrEmpty(receiver) && !IsSelfReference(receiver)) label += " (" + receiver + ")";
                    if (call.Groups["await"].Success) label = "Await " + label;
                    return Truncate(label);
                }
            }

            var declaration = DeclarationPattern.Match(value);
            if (declaration.Success && !value.StartsWith("return", StringComparison.Ordinal))
            {
                return Truncate("Set " + declaration.Groups["name"].Value + " = " + declaration.Groups["rhs"].Value);
            }

            var assign = AssignPattern.Match(value);
            if (assign.Success)
            {
                string op = assign.Groups["op"].Value;
                string lhs = SourceScanner.Collapse(assign.Groups["lhs"].Value);
                return Truncate((op == "=" ? "Set " : "Update ") + lhs + " " + op + " " + assign.Groups["rhs"].Value);
            }

            var step = StepPattern.Match(value);
            if (step.Success)
            {
                string name = step.Groups["name"].Success ? step.Groups["name"].Value : step.Groups["name2"].Value;
                string op = step.Groups["op"].Success ? step.Groups["op"].Value : step.Groups["op2"].Value;
                return Truncate((op == "++" ? "Increase " : "Decrease ") + name);
            }

            return Truncate(value);
        }

        /// <summary>"sendAsync" -> "Send Async"; "save" -> "Save".</summary>
        public static string Humanize(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (c == '_') { sb.Append(' '); continue; }

                if (i > 0 && char.IsUpper(c) && !char.IsUpper(identifier[i - 1])) sb.Append(' ');
                sb.Append(c);
            }

            string result = SourceScanner.Collapse(sb.ToString());
            if (result.Length > 0) result = char.ToUpperInvariant(result[0]) + result.Substring(1);
            return result;
        }

        /// <summary>Chi giu lai cac tham so don gian (dinh danh / so) de nhan khong bi dai.</summary>
        private static string SimpleArguments(string args)
        {
            string value = SourceScanner.Collapse(args);
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var parts = new List<string>();
            foreach (var raw in value.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                if (!Regex.IsMatch(part, @"^[A-Za-z_][\w]*$") && !Regex.IsMatch(part, @"^-?\d+(\.\d+)?$"))
                {
                    return string.Empty;
                }
                parts.Add(part);
            }

            if (parts.Count == 0 || parts.Count > 3) return string.Empty;
            return string.Join(" ", parts);
        }

        private static string LastIdentifier(string text)
        {
            var matches = Regex.Matches(SourceScanner.Collapse(text), @"[A-Za-z_]\w*");
            return matches.Count > 0 ? matches[matches.Count - 1].Value : SourceScanner.Collapse(text);
        }

        private static bool IsSelfReference(string receiver)
        {
            return receiver == "this" || receiver == "base" || receiver == "super";
        }

        private static bool IsControlWord(string name)
        {
            switch (name)
            {
                case "if":
                case "while":
                case "for":
                case "foreach":
                case "switch":
                case "catch":
                case "return":
                case "throw":
                case "using":
                case "lock":
                case "synchronized":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>"OverloadException" -> "Overload"; "Exception" giu nguyen.</summary>
        public static string ShortenExceptionName(string type)
        {
            string value = type ?? string.Empty;
            int dot = value.LastIndexOf('.');
            if (dot >= 0) value = value.Substring(dot + 1);

            foreach (string suffix in new[] { "Exception", "Error" })
            {
                if (value.Length > suffix.Length && value.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return value.Substring(0, value.Length - suffix.Length);
                }
            }
            return value;
        }

        public static string ExtractThrownType(string text)
        {
            var match = Regex.Match(text ?? string.Empty, @"throw\s+new\s+([A-Za-z_][\w.]*)");
            if (!match.Success) return string.Empty;

            string full = match.Groups[1].Value;
            int dot = full.LastIndexOf('.');
            return dot >= 0 ? full.Substring(dot + 1) : full;
        }
    }
}
