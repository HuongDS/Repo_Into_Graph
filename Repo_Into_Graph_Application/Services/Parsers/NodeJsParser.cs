using System.Text.RegularExpressions;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Method;

namespace Repo_Into_Graph_Application.Services.Parsers;

/// <summary>
/// Node.js / Express / TypeScript parser.
/// Handles JavaScript (.js, .mjs, .cjs) and TypeScript (.ts) files.
/// Uses regex/heuristics to extract Express routes, class methods,
/// arrow functions, and inter-function calls.
/// </summary>
public class NodeJsParser : ILanguageParser
{
    public string LanguageName => "Node.js (Express)";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".js", ".ts", ".mjs", ".cjs" };

    // ─── Express route registration ──────────────────────────────────────────
    // Matches: router.get('/path', handler), app.post('/api', ...)
    private static readonly Regex RouteRegex = new(
        @"(?:router|app|server)\.(get|post|put|delete|patch|options|use)\s*\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ─── Class declaration ───────────────────────────────────────────────────
    private static readonly Regex ClassRegex = new(
        @"(?:export\s+)?(?:abstract\s+)?class\s+(\w+)",
        RegexOptions.Compiled);

    // ─── Regular function declaration ────────────────────────────────────────
    private static readonly Regex FuncDeclRegex = new(
        @"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    // ─── Arrow function / const assignment ──────────────────────────────────
    // Matches: const funcName = async (...) => { or const funcName = (...) => {
    // Also: export const funcName = ...
    private static readonly Regex ArrowFuncRegex = new(
        @"(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\s*(?:\([^)]*\)|\w+)\s*=>",
        RegexOptions.Compiled);

    // ─── Class method ────────────────────────────────────────────────────────
    // Matches: async methodName( or methodName( inside a class
    private static readonly Regex ClassMethodRegex = new(
        @"^\s+(?:async\s+)?(\w+)\s*\([^)]*\)\s*(?::\s*\S+)?\s*\{",
        RegexOptions.Compiled);

    // ─── TypeScript decorator for NestJS controllers ─────────────────────────
    private static readonly Regex NestDecoratorRegex = new(
        @"@(Controller|Get|Post|Put|Delete|Patch|Injectable|Service|Module|Guard)\s*(?:\([^)]*\))?",
        RegexOptions.Compiled);

    // ─── Function call: someObj.method( or func( ────────────────────────────
    private static readonly Regex CallRegex = new(
        @"(?:(?:await|return|=|,|\(|\{|;)\s*)?(?:(\w+)\.)?(\w+)\s*\(",
        RegexOptions.Compiled);

    // ─── JS/TS builtins to skip ───────────────────────────────────────────────
    private static readonly HashSet<string> _jsBuiltins = new(StringComparer.Ordinal)
    {
        "console", "JSON", "Math", "Date", "Array", "Object", "String",
        "Number", "Boolean", "Promise", "Error", "Map", "Set", "Symbol",
        "parseInt", "parseFloat", "isNaN", "isFinite", "encodeURI", "decodeURI",
        "setTimeout", "setInterval", "clearTimeout", "clearInterval",
        "require", "import", "export", "module", "process", "Buffer",
        "log", "error", "warn", "info", "debug",
        "then", "catch", "finally", "resolve", "reject", "all", "race",
        "push", "pop", "shift", "unshift", "splice", "slice", "concat",
        "map", "filter", "reduce", "forEach", "find", "findIndex", "some", "every",
        "toString", "valueOf", "hasOwnProperty", "keys", "values", "entries",
        "assign", "create", "freeze", "defineProperty",
        "parse", "stringify", "send", "json", "status", "next",
        "if", "else", "for", "while", "do", "switch", "case", "return",
        "try", "catch", "throw", "new", "delete", "typeof", "instanceof",
        "void", "in", "of", "break", "continue", "const", "let", "var",
        "async", "await", "yield", "super", "this", "null", "undefined",
        "true", "false", "class", "extends", "implements", "interface",
        "type", "enum", "namespace", "declare", "abstract", "override",
        "get", "set", "from", "as", "default"
    };

    public Task<ExtractionResult> ParseAsync(string filePath, string sourceCode)
    {
        var result = new ExtractionResult();
        var lines = sourceCode.Split('\n');

        // Module name = file name (Node.js convention: camelCase files)
        var moduleName = Path.GetFileNameWithoutExtension(filePath);
        moduleName = ToPascalCase(moduleName);

        string currentClass = moduleName;
        bool inClass = false;
        int classIndentDepth = 0;
        string? pendingNestVerb = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            int braceDepth = CountBraceDepth(lines, 0, i);

            if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

            // ── NestJS decorator ───────────────────────────────────────────
            var nestMatch = NestDecoratorRegex.Match(trimmed);
            if (nestMatch.Success)
            {
                var decorator = nestMatch.Groups[1].Value;
                if (decorator is "Get" or "Post" or "Put" or "Delete" or "Patch")
                    pendingNestVerb = decorator.ToUpper();
                continue;
            }

            // ── Express route registration ─────────────────────────────────
            var routeMatch = RouteRegex.Match(trimmed);
            if (routeMatch.Success)
            {
                var verb = routeMatch.Groups[1].Value.ToUpper();
                var routePath = routeMatch.Groups[2].Value;
                var routeMethodName = $"{verb} {routePath}";

                result.CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = currentClass,
                    CallerMethod = "__CLASS__",
                    CalleeClass = currentClass,
                    CalleeMethod = routeMethodName,
                    Language = LanguageName
                });
                continue;
            }

            // ── Class declaration ──────────────────────────────────────────
            var classMatch = ClassRegex.Match(trimmed);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                inClass = true;
                classIndentDepth = braceDepth;
                pendingNestVerb = null;
                continue;
            }

            // ── Regular function ───────────────────────────────────────────
            var funcMatch = FuncDeclRegex.Match(trimmed);
            if (funcMatch.Success)
            {
                var funcName = funcMatch.Groups[1].Value;
                if (_jsBuiltins.Contains(funcName)) { pendingNestVerb = null; continue; }

                var displayName = pendingNestVerb != null ? $"{pendingNestVerb} {funcName}" : funcName;
                var body = ExtractBlock(lines, i);

                AddMethodNode(result, currentClass, funcName, displayName, body);
                pendingNestVerb = null;
                continue;
            }

            // ── Arrow function ─────────────────────────────────────────────
            var arrowMatch = ArrowFuncRegex.Match(trimmed);
            if (arrowMatch.Success)
            {
                var funcName = arrowMatch.Groups[1].Value;
                if (_jsBuiltins.Contains(funcName)) { pendingNestVerb = null; continue; }

                var displayName = pendingNestVerb != null ? $"{pendingNestVerb} {funcName}" : funcName;
                var body = ExtractBlock(lines, i);

                AddMethodNode(result, currentClass, funcName, displayName, body);
                pendingNestVerb = null;
                continue;
            }

            // ── Class method (indented inside class body) ──────────────────
            if (inClass)
            {
                var methodMatch = ClassMethodRegex.Match(line);
                if (methodMatch.Success)
                {
                    var methodName = methodMatch.Groups[1].Value;
                    if (_jsBuiltins.Contains(methodName) || methodName == "constructor") { pendingNestVerb = null; continue; }

                    var displayName = pendingNestVerb != null ? $"{pendingNestVerb} {methodName}" : methodName;
                    var body = ExtractBlock(lines, i);

                    AddMethodNode(result, currentClass, methodName, displayName, body);
                    pendingNestVerb = null;
                    continue;
                }
            }
        }

        return Task.FromResult(result);
    }

    private void AddMethodNode(ExtractionResult result, string currentClass, string methodName, string displayName, string body)
    {
        result.MethodSources.Add(new MethodSource
        {
            ClassName = currentClass,
            MethodName = methodName,
            SourceCode = body,
            Language = LanguageName
        });

        result.CallGraphEdges.Add(new CallGraphEdge
        {
            CallerClass = currentClass,
            CallerMethod = "__CLASS__",
            CalleeClass = currentClass,
            CalleeMethod = displayName,
            Language = LanguageName
        });

        ExtractCalls(body, currentClass, displayName, result);
    }

    private void ExtractCalls(string body, string currentClass, string currentFunc, ExtractionResult result)
    {
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

            var matches = CallRegex.Matches(trimmed);
            foreach (Match m in matches)
            {
                var objectName = m.Groups[1].Value;
                var calledFunc = m.Groups[2].Value;

                if (string.IsNullOrEmpty(calledFunc)) continue;
                if (_jsBuiltins.Contains(calledFunc)) continue;
                if (_jsBuiltins.Contains(objectName)) continue;
                if (char.IsUpper(calledFunc[0]) && string.IsNullOrEmpty(objectName)) continue; // constructor

                var calleeClass = string.IsNullOrEmpty(objectName)
                    ? currentClass
                    : ToPascalCase(objectName);

                if (calleeClass == currentClass && calledFunc == currentFunc.Split(' ').Last()) continue;

                result.CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = currentClass,
                    CallerMethod = currentFunc,
                    CalleeClass = calleeClass,
                    CalleeMethod = calledFunc,
                    Language = LanguageName
                });
            }
        }
    }

    private static string ExtractBlock(string[] lines, int startLine)
    {
        var sb = new System.Text.StringBuilder();
        int braceCount = 0;
        bool started = false;

        for (int i = startLine; i < lines.Length && i < startLine + 200; i++)
        {
            var line = lines[i];
            sb.AppendLine(line);

            // Arrow functions without braces: single expression
            if (!started && line.Contains("=>") && !line.Contains("{"))
            {
                break;
            }

            foreach (var ch in line)
            {
                if (ch == '{') { braceCount++; started = true; }
                else if (ch == '}') braceCount--;
            }

            if (started && braceCount == 0) break;
        }

        return sb.ToString();
    }

    private static int CountBraceDepth(string[] lines, int from, int to)
    {
        int depth = 0;
        for (int i = from; i < to && i < lines.Length; i++)
        {
            foreach (var ch in lines[i])
            {
                if (ch == '{') depth++;
                else if (ch == '}') depth--;
            }
        }
        return depth;
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Handle kebab-case and camelCase file names
        var parts = name.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return char.ToUpper(name[0]) + name[1..];
        return string.Concat(parts.Select(p => p.Length > 0 ? char.ToUpper(p[0]) + p[1..] : ""));
    }
}





