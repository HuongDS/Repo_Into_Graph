using System.Text.RegularExpressions;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Method;

namespace Repo_Into_Graph_Application.Services.Parsers;

/// <summary>
/// Java parser for Spring Boot projects.
/// Uses regex/heuristics to extract classes, methods (including REST endpoints),
/// and method calls from Java source files.
/// </summary>
public class JavaParser : ILanguageParser
{
    public string LanguageName => "Java (Spring Boot)";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".java" };

    /// <summary>
    /// Caller gia dung de danh dau "day la diem vao cua class" (endpoint HTTP hoac method
    /// public cua Spring bean). Day KHONG phai mot method co that, nen BusinessFlowParser
    /// phai loai no ra khoi danh sach ung vien entry point — neu khong, EntryPoint cua
    /// Feature se thanh "SurveyController.__CLASS__" thay vi "SurveyController.getSurveys",
    /// va khong the khop voi template_business.json.
    /// </summary>
    public const string EntryMarker = "__CLASS__";

    // ─── Class-level Spring annotations ─────────────────────────────────────
    private static readonly Regex SpringClassAnnotationRegex = new(
        @"@(RestController|Controller|Service|Repository|Component|Configuration|RestControllerAdvice)\b",
        RegexOptions.Compiled);

    // ─── Class declaration ───────────────────────────────────────────────────
    private static readonly Regex ClassRegex = new(
        @"(?:public|protected|private)?\s*(?:abstract\s+)?class\s+(\w+)",
        RegexOptions.Compiled);

    // ─── HTTP method annotations (Spring MVC) ────────────────────────────────
    private static readonly Regex HttpAnnotationRegex = new(
        @"@(GetMapping|PostMapping|PutMapping|DeleteMapping|PatchMapping|RequestMapping)(?:\s*\(([^)]*)\))?",
        RegexOptions.Compiled);

    // ─── Method declaration ──────────────────────────────────────────────────
    private static readonly Regex MethodRegex = new(
        @"(?:public|protected|private)\s+(?:static\s+)?(?:[\w<>\[\]?,\s]+)\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    // ─── Method call: someObject.method( OR ClassName.method( OR method( ────
    private static readonly Regex MethodCallRegex = new(
        @"(?:(\w+)\.)?(\w+)\s*\(",
        RegexOptions.Compiled);

    // ─── Annotations that indicate a method is a Spring component ────────────
    private static readonly Regex MethodAnnotationRegex = new(
        @"@(Transactional|Async|Scheduled|EventListener|Override)\b",
        RegexOptions.Compiled);

    // ─── Cau truc re nhanh -> NodeType.DecisionGateway ───────────────────────
    // Giu DUNG tap cau truc ma CSharpParser.cs:260 dem, de GatewaysCount so sanh duoc
    // giua hai ngon ngu: if / switch / while / do / for (bao gom ca enhanced-for cua
    // Java, cung tu khoa "for") / toan tu ba ngoi. KHONG dem catch, vi ben C# cung khong.
    //
    // Truoc day JavaParser khong gan Type nen moi method Java deu la NodeType.Activity
    // (gia tri mac dinh), khien GatewaysCount LUON = 0 voi repo Java — chi so nay mat
    // hoan toan kha nang do luong, va PathType luon bi xep la "Happy Path".
    private static readonly Regex BranchRegex = new(
        @"\bif\s*\(|\bswitch\s*\(|\bwhile\s*\(|\bfor\s*\(|\bdo\s*\{|\?[^;{}\n]{1,120}:",
        RegexOptions.Compiled);

    // ─── Noise: Java built-in calls to skip ──────────────────────────────────
    private static readonly HashSet<string> _javaBuiltinCalls = new(StringComparer.Ordinal)
    {
        "System", "Math", "String", "Integer", "Long", "Double", "Boolean",
        "List", "Map", "Set", "Optional", "Arrays", "Collections", "Objects",
        "StringBuilder", "StringBuffer", "Thread", "Object", "Class",
        "println", "print", "format", "valueOf", "toString", "equals",
        "hashCode", "getClass", "instanceof", "super", "this",
        "of", "ofNullable", "get", "set", "isEmpty", "isPresent",
        "size", "add", "remove", "put", "containsKey", "stream",
        "filter", "map", "collect", "toList", "forEach", "orElse",
        "orElseThrow", "orElseGet", "ifPresent"
    };

    public Task<ExtractionResult> ParseAsync(string filePath, string sourceCode)
    {
        var result = new ExtractionResult();
        var lines = sourceCode.Split('\n');

        string currentClass = Path.GetFileNameWithoutExtension(filePath);
        bool isSpringClass = SpringClassAnnotationRegex.IsMatch(sourceCode);

        // Find actual class name from source
        foreach (var line in lines)
        {
            var classMatch = ClassRegex.Match(line);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                break;
            }
        }

        // Parse methods
        string? pendingHttpVerb = null;
        string? pendingAnnotation = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Detect HTTP mapping annotation
            var httpMatch = HttpAnnotationRegex.Match(line);
            if (httpMatch.Success)
            {
                pendingHttpVerb = MapHttpVerb(httpMatch.Groups[1].Value);
                pendingAnnotation = line;
                continue;
            }

            // Detect method declaration
            var methodMatch = MethodRegex.Match(line);
            if (methodMatch.Success)
            {
                var methodName = methodMatch.Groups[1].Value;

                // Skip constructor (same name as class), Java keywords
                if (methodName == currentClass || IsJavaKeyword(methodName))
                {
                    pendingHttpVerb = null;
                    pendingAnnotation = null;
                    continue;
                }

                // Extract method body lines until matching closing brace
                var methodBody = ExtractMethodBody(lines, i);

                // Store method source
                bool isDecisionGateway = BranchRegex.IsMatch(methodBody);

                result.MethodSources.Add(new MethodSource
                {
                    ClassName = currentClass,
                    MethodName = methodName,
                    SourceCode = methodBody,
                    Language = LanguageName,
                    Type = isDecisionGateway
                        ? Repo_Into_Graph_DataAccess.Consts.NodeType.DecisionGateway
                        : Repo_Into_Graph_DataAccess.Consts.NodeType.Activity
                });

                // LUU Y VE TEN METHOD TRONG CALL GRAPH:
                // Truoc day cho nay dung `displayName` = "GET getSurveys" (co tien to HTTP verb)
                // cho CalleeMethod/CallerMethod, TRONG KHI MethodSources.MethodName luu ten thuan
                // "getSurveys". Hai ben khong bao gio khop nhau, nen GraphMapperService dung
                // BuildKey(class, method) tra ra hai khoa khac nhau:
                //     methodIdLookup : "surveycontroller.getsurveys"
                //     graphLookup    : "surveycontroller.get getsurveys"
                // => FindAllMethodsInSubTree khong duyet duoc canh nao, FeatureMethodMapping rong,
                //    va moi API sinh cau hoi deu khong tim thay source code.
                //
                // CSharpParser da xu ly dung viec nay tu truoc (xem CSharpParser.cs:301 —
                // "Dung ten thuan tuy dong bo") nhung chua duoc port sang day.
                // HTTP verb chi mang tinh hien thi, khong duoc phep di vao khoa dinh danh.

                // Add entry node if it's an HTTP endpoint or Spring component method
                if (isSpringClass && (pendingHttpVerb != null || IsPublicMethod(line)))
                {
                    result.CallGraphEdges.Add(new CallGraphEdge
                    {
                        CallerClass = currentClass,
                        CallerMethod = EntryMarker,
                        CalleeClass = currentClass,
                        CalleeMethod = methodName,
                        Language = LanguageName
                    });
                }

                // Extract calls from method body
                ExtractMethodCalls(methodBody, currentClass, methodName, result);

                pendingHttpVerb = null;
                pendingAnnotation = null;
                continue;
            }

            // Reset pending annotation if we hit something else (not blank, not annotation)
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("@") && !line.StartsWith("//") && !line.StartsWith("*"))
            {
                pendingHttpVerb = null;
                pendingAnnotation = null;
            }
        }

        return Task.FromResult(result);
    }

    private void ExtractMethodCalls(string methodBody, string currentClass, string currentMethod, ExtractionResult result)
    {
        var bodyLines = methodBody.Split('\n');
        foreach (var line in bodyLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            var matches = MethodCallRegex.Matches(trimmed);
            foreach (Match m in matches)
            {
                var objectName = m.Groups[1].Value;
                var calledMethod = m.Groups[2].Value;

                if (string.IsNullOrEmpty(calledMethod)) continue;
                if (IsJavaKeyword(calledMethod)) continue;
                if (_javaBuiltinCalls.Contains(calledMethod)) continue;
                if (_javaBuiltinCalls.Contains(objectName)) continue;
                if (char.IsLower(calledMethod[0]) && string.IsNullOrEmpty(objectName)) continue; // skip local vars

                // Determine callee class
                var calleeClass = string.IsNullOrEmpty(objectName)
                    ? currentClass
                    : InferClassName(objectName);

                // Bo canh tu-goi-chinh-minh. currentMethod gio la ten thuan (khong con tien to
                // HTTP verb) nen so sanh truc tiep, khong can Split(' ').Last() nhu truoc.
                if (calleeClass == currentClass && calledMethod == currentMethod) continue;

                result.CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = currentClass,
                    CallerMethod = currentMethod,
                    CalleeClass = calleeClass,
                    CalleeMethod = calledMethod,
                    Language = LanguageName
                });
            }
        }
    }

    private static string ExtractMethodBody(string[] lines, int methodStartLine)
    {
        var sb = new System.Text.StringBuilder();
        int braceCount = 0;
        bool started = false;

        for (int i = methodStartLine; i < lines.Length && i < methodStartLine + 200; i++)
        {
            var line = lines[i];
            sb.AppendLine(line);

            foreach (var ch in line)
            {
                if (ch == '{') { braceCount++; started = true; }
                else if (ch == '}') braceCount--;
            }

            if (started && braceCount == 0) break;
        }

        return sb.ToString();
    }

    private static string MapHttpVerb(string annotation) => annotation switch
    {
        "GetMapping" => "GET",
        "PostMapping" => "POST",
        "PutMapping" => "PUT",
        "DeleteMapping" => "DELETE",
        "PatchMapping" => "PATCH",
        "RequestMapping" => "HTTP",
        _ => "HTTP"
    };

    private static string InferClassName(string objectName)
    {
        // Convention: if variable starts with lowercase, try to guess class by capitalizing
        // e.g. userService -> UserService, orderRepo -> OrderRepo
        if (string.IsNullOrEmpty(objectName)) return "Unknown";
        return char.ToUpper(objectName[0]) + objectName[1..];
    }

    private static bool IsPublicMethod(string line)
        => line.TrimStart().StartsWith("public ");

    private static bool IsJavaKeyword(string name) => name is
        "if" or "else" or "for" or "while" or "do" or "switch" or "case" or "return"
        or "try" or "catch" or "finally" or "throw" or "throws" or "new" or "import"
        or "package" or "class" or "interface" or "enum" or "extends" or "implements"
        or "static" or "final" or "abstract" or "synchronized" or "volatile" or "transient"
        or "void" or "int" or "long" or "double" or "float" or "boolean" or "char"
        or "byte" or "short" or "null" or "true" or "false" or "assert";
}





