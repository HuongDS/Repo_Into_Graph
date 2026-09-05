using System;
using System.Collections.Generic;
using System.Text;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Tien xu ly ma nguon: che (mask) comment va chuoi ky tu bang dau cach
    /// de viec quet cau truc (dau ; { } ( )) khong bi nhieu boi noi dung chuoi.
    /// Chuoi Masked co DO DAI BANG chuoi goc nen moi chi so deu anh xa 1-1.
    /// </summary>
    internal sealed class SourceScanner
    {
        /// <summary>Ma nguon goc (giu nguyen de cat lay text hien thi).</summary>
        public string Source { get; }

        /// <summary>Ma nguon da che comment/chuoi - dung de quet cau truc.</summary>
        public string Masked { get; }

        private readonly List<int> _lineStarts = new();

        public SourceScanner(string source)
        {
            Source = source ?? string.Empty;
            Masked = Mask(Source);

            _lineStarts.Add(0);
            for (int i = 0; i < Source.Length; i++)
            {
                if (Source[i] == '\n') _lineStarts.Add(i + 1);
            }
        }

        public int Length => Source.Length;

        /// <summary>Tra ve so dong (1-based) cua mot chi so ky tu.</summary>
        public int LineOf(int index)
        {
            if (index < 0) index = 0;
            if (index > Source.Length) index = Source.Length;

            int lo = 0, hi = _lineStarts.Count - 1, ans = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_lineStarts[mid] <= index) { ans = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ans + 1;
        }

        public string Slice(int start, int endExclusive)
        {
            if (start < 0) start = 0;
            if (endExclusive > Source.Length) endExclusive = Source.Length;
            if (endExclusive <= start) return string.Empty;
            return Source.Substring(start, endExclusive - start);
        }

        /// <summary>Bo qua khoang trang (comment da bi che thanh khoang trang).</summary>
        public int SkipTrivia(int pos, int limit)
        {
            while (pos < limit && pos < Masked.Length && char.IsWhiteSpace(Masked[pos])) pos++;
            return pos;
        }

        /// <summary>Lui ve ky tu khong phai khoang trang gan nhat.</summary>
        public int SkipTriviaBack(int pos)
        {
            while (pos >= 0 && char.IsWhiteSpace(Masked[pos])) pos--;
            return pos;
        }

        /// <summary>
        /// Tim ky tu dong tuong ung voi ( [ { tai vi tri openIndex.
        /// Tra ve -1 neu khong tim thay.
        /// </summary>
        public int FindMatching(int openIndex, int limit)
        {
            if (openIndex < 0 || openIndex >= Masked.Length) return -1;
            char open = Masked[openIndex];
            char close = open switch
            {
                '(' => ')',
                '[' => ']',
                '{' => '}',
                _ => '\0'
            };
            if (close == '\0') return -1;

            int depth = 0;
            if (limit > Masked.Length) limit = Masked.Length;

            for (int i = openIndex; i < limit; i++)
            {
                char c = Masked[i];
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>Doc mot tu dinh danh bat dau tai pos (rong neu khong phai dinh danh).</summary>
        public string PeekWord(int pos, out int wordEnd)
        {
            wordEnd = pos;
            if (pos < 0 || pos >= Masked.Length) return string.Empty;
            if (!IsIdentStart(Masked[pos])) return string.Empty;

            int i = pos;
            while (i < Masked.Length && IsIdentChar(Masked[i])) i++;
            wordEnd = i;
            return Source.Substring(pos, i - pos);
        }

        public static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';

        public static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

        /// <summary>Kiem tra pos co phai la dau mot tu (khong dinh vao tu truoc do).</summary>
        public bool IsWordBoundary(int pos)
        {
            if (pos <= 0) return true;
            return !IsIdentChar(Masked[pos - 1]);
        }

        /// <summary>Tim ky tu target o do sau ngoac = 0 trong pham vi [start, limit).</summary>
        public int IndexOfAtDepthZero(int start, int limit, char target)
        {
            int par = 0, brk = 0, brc = 0;
            if (limit > Masked.Length) limit = Masked.Length;

            for (int i = start; i < limit; i++)
            {
                char c = Masked[i];
                switch (c)
                {
                    case '(': par++; break;
                    case ')': par--; break;
                    case '[': brk++; break;
                    case ']': brk--; break;
                    case '{': brc++; break;
                    case '}': brc--; break;
                }
                if (c == target && par <= 0 && brk <= 0 && brc <= 0) return i;
            }
            return -1;
        }

        /// <summary>Chuan hoa khoang trang: gop nhieu khoang trang / xuong dong thanh 1 dau cach.</summary>
        public static string Collapse(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool lastSpace = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastSpace) { sb.Append(' '); lastSpace = true; }
                }
                else { sb.Append(c); lastSpace = false; }
            }
            return sb.ToString().Trim();
        }

        // -------------------------------------------------------------
        // MASKING
        // -------------------------------------------------------------
        private static string Mask(string src)
        {
            int n = src.Length;
            var buf = src.ToCharArray();
            int i = 0;

            while (i < n)
            {
                char c = src[i];

                // Comment mot dong
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') { buf[i] = ' '; i++; }
                    continue;
                }

                // Comment nhieu dong
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    buf[i] = ' ';
                    buf[i + 1] = ' ';
                    i += 2;
                    while (i < n && !(src[i] == '*' && i + 1 < n && src[i + 1] == '/'))
                    {
                        if (src[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    if (i < n) { buf[i] = ' '; i++; }
                    if (i < n) { buf[i] = ' '; i++; }
                    continue;
                }

                // Chuoi verbatim C#: @"..."
                if (c == '@' && i + 1 < n && src[i + 1] == '"')
                {
                    buf[i] = ' ';
                    buf[i + 1] = ' ';
                    i += 2;
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { buf[i] = ' '; buf[i + 1] = ' '; i += 2; continue; }
                            buf[i] = ' '; i++;
                            break;
                        }
                        if (src[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    continue;
                }

                // Text block (Java) / raw string (C#): """ ... """
                if (c == '"' && i + 2 < n && src[i + 1] == '"' && src[i + 2] == '"')
                {
                    buf[i] = ' '; buf[i + 1] = ' '; buf[i + 2] = ' ';
                    i += 3;
                    while (i < n)
                    {
                        if (src[i] == '"' && i + 2 < n && src[i + 1] == '"' && src[i + 2] == '"')
                        {
                            buf[i] = ' '; buf[i + 1] = ' '; buf[i + 2] = ' ';
                            i += 3;
                            break;
                        }
                        if (src[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    continue;
                }

                // Chuoi thuong va ky tu
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    buf[i] = ' ';
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\')
                        {
                            buf[i] = ' ';
                            if (i + 1 < n && src[i + 1] != '\n') buf[i + 1] = ' ';
                            i += 2;
                            continue;
                        }
                        if (src[i] == quote) { buf[i] = ' '; i++; break; }
                        if (src[i] == '\n') break; // chuoi khong dong -> dung o cuoi dong
                        buf[i] = ' ';
                        i++;
                    }
                    continue;
                }

                i++;
            }

            return new string(buf);
        }
    }
}
