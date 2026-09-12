using System;
using System.Text.RegularExpressions;

namespace Repo_Into_Graph_Application.Helper
{
    /// <summary>
    /// Chuan hoa ten ngon ngu ve dang canonical ("csharp" | "java") — dang duy nhat ma
    /// ca Python Microservice lan markdown code fence deu hieu duoc.
    ///
    /// VI SAO CAN LOP NAY:
    /// 1. Cac ILanguageParser dat LanguageName la TEN HIEN THI ("C# (.NET)",
    ///    "Java (Spring Boot)"). Chuoi nay KHONG dung truc tiep duoc cho
    ///    POST /api/analyze-context (chi chap nhan java|csharp|c#|dotnet) cung nhu
    ///    cho ```fence trong prompt gui LLM.
    /// 2. Cac ban ghi MethodSourceRecord tao ra TRUOC khi co cot Language deu null.
    ///    Bat nguoi dung phan tich lai toan bo repo chi de co cot nay la khong can
    ///    thiet — bo suy luan tu chinh ma nguon o duoi xu ly duoc cac ban ghi cu.
    /// 3. Tang 1 va Tang 2 chi ho tro Java + C#. Cac ngon ngu khac (Python, Node.js)
    ///    phai duoc nhan dien de BO QUA Python Microservice thay vi gui len roi nhan
    ///    HTTP 400 lam hong ca vong lap sinh cau hoi.
    /// </summary>
    public static class LanguageNormalizer
    {
        public const string CSharp = "csharp";
        public const string Java = "java";

        /// <summary>
        /// Chuan hoa ngon ngu. Uu tien nhan hien co; neu nhan rong/khong ro thi suy luan
        /// tu chinh ma nguon.
        /// </summary>
        /// <param name="declaredLanguage">
        /// Gia tri cot MethodSourceRecord.Language (co the null voi ban ghi cu),
        /// hoac ILanguageParser.LanguageName, hoac duoi file.
        /// </param>
        /// <param name="sourceCode">Ma nguon dung de suy luan khi nhan khong xac dinh.</param>
        /// <param name="canonical">"csharp" hoac "java". Mac dinh "csharp" khi tra ve false.</param>
        /// <returns>true neu xac dinh duoc la mot trong hai ngon ngu duoc ho tro.</returns>
        public static bool TryNormalize(string? declaredLanguage, string? sourceCode, out string canonical)
        {
            var fromLabel = FromLabel(declaredLanguage);
            if (fromLabel != null)
            {
                canonical = fromLabel;
                return true;
            }

            var detected = DetectFromSource(sourceCode);
            if (detected != null)
            {
                canonical = detected;
                return true;
            }

            canonical = CSharp; // gia tri an toan cho caller nao khong kiem tra ket qua tra ve
            return false;
        }

        /// <summary>
        /// Phien ban tien dung: luon tra ve mot ngon ngu (mac dinh "csharp" neu khong ro).
        /// Chi dung o noi ma viec doan sai chi anh huong den the ```fence, khong anh huong
        /// den viec chon parser.
        /// </summary>
        public static string Normalize(string? declaredLanguage, string? sourceCode = null)
        {
            TryNormalize(declaredLanguage, sourceCode, out var canonical);
            return canonical;
        }

        /// <summary>Kiem tra nhanh mot ngon ngu co nam trong pham vi Tang 1 + Tang 2 khong.</summary>
        public static bool IsSupported(string? declaredLanguage, string? sourceCode = null)
            => TryNormalize(declaredLanguage, sourceCode, out _);

        // ------------------------------------------------------------------
        // 1. Nhan dien tu NHAN (nhanh, chac chan nhat)
        // ------------------------------------------------------------------
        private static string? FromLabel(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var s = raw.Trim().ToLowerInvariant();

            // Loai TRUOC cac ngon ngu chua ho tro nhung ten co chua chuoi "java".
            // Neu khong, "javascript" se bi nhan nham thanh "java".
            if (s.Contains("javascript") || s.Contains("typescript")
                || s.Contains("node") || s.Contains(".js") || s.Contains(".ts"))
                return null;
            if (s.Contains("python") || s.Contains(".py")) return null;

            if (s.Contains("java") || s == ".java") return Java;

            if (s.Contains("c#") || s.Contains("csharp") || s.Contains("dotnet")
                || s.Contains(".net") || s == "cs" || s == ".cs")
                return CSharp;

            return null;
        }

        // ------------------------------------------------------------------
        // 2. Suy luan tu MA NGUON (cho cac ban ghi cu khong co nhan)
        //    Cham diem hai chieu roi chon ben cao hon — ben vung hon viec dua vao
        //    mot dau hieu duy nhat, vi mot method ngan co the thieu bat ky dau hieu nao.
        // ------------------------------------------------------------------
        private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private const RegexOptions MultiOpts = Opts | RegexOptions.Multiline;

        private static readonly (Regex Pattern, int Weight)[] JavaSignals =
        {
            // Annotation dau dong: dac trung manh nhat cua Java/Spring
            (new Regex(@"^\s*@(Override|Autowired|Service|Component|Repository|RestController|Controller|Transactional|RequestMapping|GetMapping|PostMapping|PutMapping|DeleteMapping|PatchMapping|Bean|Entity|Valid|SneakyThrows|Slf4j|Data)\b", MultiOpts), 5),
            (new Regex(@"\bimport\s+javax?\.", Opts), 5),
            (new Regex(@"\bSystem\.out\.print", Opts), 4),
            (new Regex(@"\)\s*throws\s+\w", Opts), 4),          // chi Java co 'throws' trong chu ky ham
            (new Regex(@"\bboolean\b", Opts), 4),               // C# dung 'bool'
            (new Regex(@"\bimplements\s+\w", Opts), 4),         // C# dung dau ':'
            (new Regex(@"\bextends\s+\w", Opts), 3),
            (new Regex(@"->\s*[\{\w\(]", Opts), 3),             // lambda Java (C# dung '=>')
            (new Regex(@"\.stream\(\)", Opts), 3),
            (new Regex(@"\bOptional<", Opts), 3),
            (new Regex(@"\bArrayList<|\bHashMap<", Opts), 2),
            (new Regex(@"\bString\s+\w+\s*[=;,\)]", Opts), 2),  // 'String' hoa (C# dung 'string')
            (new Regex(@"\bfinal\s+\w+\s+\w+\s*=", Opts), 2),
            (new Regex(@"\bpublic\s+static\s+void\s+main\b", Opts), 3),
        };

        private static readonly (Regex Pattern, int Weight)[] CSharpSignals =
        {
            (new Regex(@"\bnamespace\s+[\w\.]", Opts), 5),
            (new Regex(@"\busing\s+System\b", Opts), 5),
            (new Regex(@"\basync\s+Task\b", Opts), 5),
            (new Regex(@"\bawait\s+\w", Opts), 4),
            (new Regex(@"\bnameof\s*\(", Opts), 4),
            (new Regex(@"\bpublic\s+[\w<>\?\[\]]+\s+\w+\s*\{\s*get\s*;", Opts), 4), // auto-property
            (new Regex(@"^\s*\[\w+(\(|\]|\s)", MultiOpts), 3),  // attribute [HttpGet] (Java dung '@')
            (new Regex(@"\bbool\b", Opts), 4),                  // Java dung 'boolean'
            (new Regex(@"\bstring\b", Opts), 3),                // Java dung 'String'
            (new Regex(@"\breadonly\b", Opts), 3),
            (new Regex(@"\boverride\s+\w", Opts), 3),
            (new Regex(@"\bIEnumerable<|\bIList<|\bICollection<", Opts), 3),
            (new Regex(@"\bGuid\b", Opts), 3),
            (new Regex(@"\?\?", Opts), 3),                      // null-coalescing: khong co trong Java
            (new Regex(@"=>", Opts), 2),                        // lambda / expression-bodied
            (new Regex(@"\?\.", Opts), 2),                      // null-conditional
            (new Regex(@"\bvar\s+\w+\s*=", Opts), 1),
        };

        private static string? DetectFromSource(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            // Cat bot de khong quet toan bo file rat dai; dau hieu ngon ngu luon xuat hien som.
            var sample = code.Length > 8000 ? code.Substring(0, 8000) : code;

            int javaScore = 0, csharpScore = 0;
            foreach (var (pattern, weight) in JavaSignals)
                if (pattern.IsMatch(sample)) javaScore += weight;
            foreach (var (pattern, weight) in CSharpSignals)
                if (pattern.IsMatch(sample)) csharpScore += weight;

            if (javaScore == 0 && csharpScore == 0) return null;
            if (javaScore == csharpScore) return null;   // khong ket luan khi hoa

            return javaScore > csharpScore ? Java : CSharp;
        }
    }
}
