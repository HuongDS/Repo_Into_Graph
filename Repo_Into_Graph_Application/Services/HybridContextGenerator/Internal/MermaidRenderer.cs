using System;
using System.Collections.Generic;
using System.Text;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Ket xuat CFG ra cu phap Mermaid ("graph TD").
    ///
    /// Mot ham   -> viet gon kieu inline:  A[Start] --> B{Check req == null}
    /// Nhieu ham -> moi ham mot subgraph, khai bao node truoc roi den cac canh.
    /// </summary>
    internal static class MermaidRenderer
    {
        public static string Render(IReadOnlyList<CfgNodeDto> nodes, IReadOnlyList<CfgEdgeDto> edges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph TD");

            if (nodes.Count == 0)
            {
                sb.AppendLine("  A[Start] --> B[End]");
                return sb.ToString().TrimEnd();
            }

            var byId = new Dictionary<string, CfgNodeDto>(StringComparer.Ordinal);
            foreach (var node in nodes) byId[node.Id] = node;

            // Gom node theo cum lien tiep cung mot ham (ho tro ca ham trung ten - overload)
            var groups = new List<(string Method, List<CfgNodeDto> Items)>();
            foreach (var node in nodes)
            {
                string method = node.Method ?? string.Empty;
                if (groups.Count == 0 || !string.Equals(groups[groups.Count - 1].Method, method, StringComparison.Ordinal))
                {
                    groups.Add((method, new List<CfgNodeDto>()));
                }
                groups[groups.Count - 1].Items.Add(node);
            }

            var declared = new HashSet<string>(StringComparer.Ordinal);

            if (groups.Count > 1)
            {
                for (int index = 0; index < groups.Count; index++)
                {
                    string title = string.IsNullOrEmpty(groups[index].Method) ? "code" : groups[index].Method;
                    sb.AppendLine("  subgraph SG" + index + "[\"" + EscapeLabel(title) + "\"]");
                    foreach (var node in groups[index].Items)
                    {
                        sb.AppendLine("    " + Declare(node));
                        declared.Add(node.Id);
                    }
                    sb.AppendLine("  end");
                }
            }

            foreach (var edge in edges)
            {
                string from = Reference(edge.From, byId, declared);
                string to = Reference(edge.To, byId, declared);
                string label = SanitizeEdgeLabel(edge.Label);

                sb.AppendLine(string.IsNullOrEmpty(label)
                    ? "  " + from + " --> " + to
                    : "  " + from + " -- " + label + " --> " + to);
            }

            // Node co lap (khong nam tren canh nao) van phai duoc khai bao
            foreach (var node in nodes)
            {
                if (!declared.Contains(node.Id))
                {
                    sb.AppendLine("  " + Declare(node));
                    declared.Add(node.Id);
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Lan dau xuat hien thi kem hinh dang + nhan, cac lan sau chi ghi id.</summary>
        private static string Reference(string id, Dictionary<string, CfgNodeDto> byId, HashSet<string> declared)
        {
            if (declared.Contains(id) || !byId.TryGetValue(id, out var node)) return id;
            declared.Add(id);
            return Declare(node);
        }

        private static string Declare(CfgNodeDto node)
        {
            string label = EscapeLabel(node.Label);
            string body = NeedsQuote(node.Label) ? "\"" + label + "\"" : label;

            return node.Kind == "DECISION"
                ? node.Id + "{" + body + "}"
                : node.Id + "[" + body + "]";
        }

        /// <summary>
        /// Chi boc dau nhay khi nhan chua ky tu co the lam vo cu phap Mermaid
        /// ( ) [ ] { } " | # ; & \ hoac dau nhon. Nho vay "req == null?" duoc viet
        /// tran nhu quy uoc trong checklist ban giao.
        /// </summary>
        private static bool NeedsQuote(string label)
        {
            foreach (char c in label)
            {
                if (char.IsLetterOrDigit(c)) continue;
                switch (c)
                {
                    case ' ':
                    case '_':
                    case '.':
                    case '-':
                    case '?':
                    case '=':
                    case '!':
                    case ':':
                    case ',':
                    case '+':
                    case '*':
                    case '/':
                    case '%':
                        continue;
                    default:
                        return true;
                }
            }
            return false;
        }

        private static string EscapeLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "(empty)";
            return label
                .Replace("\"", "'")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string SanitizeEdgeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return string.Empty;

            var sb = new StringBuilder();
            foreach (char c in label)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '.') sb.Append(c);
                else sb.Append(' ');
            }
            return SourceScanner.Collapse(sb.ToString());
        }
    }
}
