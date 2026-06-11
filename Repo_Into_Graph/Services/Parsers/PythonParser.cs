using System.Text.RegularExpressions;
using Repo_Into_Graph.Models;

namespace Repo_Into_Graph.Services.Parsers;

/// <summary>
/// Python parser for FastAPI projects.
/// Uses regex/heuristics to extract routers, endpoint functions (via decorators),
/// helper functions, and function calls from Python source files.
/// </summary>
public class PythonParser : ILanguageParser
{
    public string LanguageName => "Python (FastAPI)";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".py" };

    // ─── FastAPI HTTP route decorators ───────────────────────────────────────
    // Matches: @app.get("/path"), @router.post("/path"), @api.put(...)
    private static readonly Regex RouteDecoratorRegex = new(
        @"@(\w+)\.(get|post|put|delete|patch|options|head)\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ─── Generic decorator (for @router, @app, etc.) ─────────────────────────
    private static readonly Regex GenericDecoratorRegex = new(
        @"^@(\w+(?:\.\w+)*)\s*(?:\(.*\))?\s*$",
        RegexOptions.Compiled);

    // ─── Class declaration ───────────────────────────────────────────────────
    private static readonly Regex ClassRegex = new(
        @"^class\s+(\w+)\s*(?:\(([^)]*)\))?:",
        RegexOptions.Compiled);

    // ─── Function/method declaration ─────────────────────────────────────────
    private static readonly Regex FuncRegex = new(
        @"^(?:async\s+)?def\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    // ─── Function call: func(...) or obj.method(...) ─────────────────────────
    private static readonly Regex CallRegex = new(
        @"(?:(\w+)\.)?(\w+)\s*\(",
        RegexOptions.Compiled);

    // ─── Python builtins/stdlib to ignore ────────────────────────────────────
    private static readonly HashSet<string> _pythonBuiltins = new(StringComparer.Ordinal)
    {
        "print", "len", "range", "enumerate", "zip", "map", "filter", "sorted",
        "list", "dict", "set", "tuple", "str", "int", "float", "bool", "bytes",
        "type", "isinstance", "issubclass", "hasattr", "getattr", "setattr",
        "open", "repr", "format", "input", "vars", "dir", "help",
        "super", "property", "staticmethod", "classmethod",
        "Exception", "ValueError", "TypeError", "KeyError", "IndexError",
        "append", "extend", "update", "items", "keys", "values",
        "split", "join", "strip", "lower", "upper", "replace", "find",
        "get", "pop", "items", "copy", "deepcopy",
        "HTTPException", "Query", "Path", "Body", "Depends", "BackgroundTasks",
        "Request", "Response", "JSONResponse", "RedirectResponse",
        "raise", "return", "yield", "await", "async", "if", "else", "elif",
        "for", "while", "with", "try", "except", "finally", "pass", "break",
        "continue", "import", "from", "as", "in", "not", "and", "or", "is",
        "lambda", "del", "global", "nonlocal", "assert", "class", "def"
    };

    public Task<ExtractionResult> ParseAsync(string filePath, string sourceCode)
    {
        var result = new ExtractionResult();
        var lines = sourceCode.Split('\n');

        // Module name = file name (Python convention)
        string currentModule = Path.GetFileNameWithoutExtension(filePath);
        string currentClass = currentModule;

        string? pendingHttpVerb = null;
        int currentIndent = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;

            if (trimmed.StartsWith("#")) continue; // skip comments

            // ── Class declaration ──────────────────────────────────────────
            var classMatch = ClassRegex.Match(trimmed);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                currentIndent = indent;
                pendingHttpVerb = null;
                continue;
            }

            // ── Route decorator ────────────────────────────────────────────
            var routeMatch = RouteDecoratorRegex.Match(trimmed);
            if (routeMatch.Success)
            {
                pendingHttpVerb = routeMatch.Groups[2].Value.ToUpper(); // GET, POST...
                continue;
            }

            // ── Function/method declaration ────────────────────────────────
            var funcMatch = FuncRegex.Match(trimmed);
            if (funcMatch.Success)
            {
                var funcName = funcMatch.Groups[1].Value;
                if (funcName.StartsWith("_") && !funcName.StartsWith("__")) { pendingHttpVerb = null; continue; } // skip private

                var displayName = pendingHttpVerb != null
                    ? $"{pendingHttpVerb} {funcName}"
                    : funcName;

                // Extract function body
                var funcBody = ExtractFunctionBody(lines, i, indent);

                result.MethodSources.Add(new MethodSource
                {
                    ClassName = currentClass,
                    MethodName = funcName,
                    SourceCode = funcBody,
                    Language = LanguageName,
                    HttpVerb = pendingHttpVerb,
                    DisplayName = displayName
                });

                // Add entry node for endpoints or public functions
                if (pendingHttpVerb != null || !funcName.StartsWith("_"))
                {
                    result.CallGraphEdges.Add(new CallGraphEdge
                    {
                        CallerClass = currentClass,
                        CallerMethod = "__CLASS__",
                        CalleeClass = currentClass,
                        CalleeMethod = funcName,
                        Language = LanguageName,
                        CallerDisplayName = "__CLASS__",
                        CalleeDisplayName = displayName
                    });
                }

                // Extract calls from body
                ExtractFunctionCalls(funcBody, currentClass, funcName, displayName, result);

                pendingHttpVerb = null;
                continue;
            }

            // Reset pending decorator if unrelated line
            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("@"))
                pendingHttpVerb = null;
        }

        return Task.FromResult(result);
    }

    private void ExtractFunctionCalls(string body, string currentClass, string currentFunc, string currentFuncDisplay, ExtractionResult result)
    {
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#") || trimmed.StartsWith("\"\"\"") || trimmed.StartsWith("'''")) continue;

            var matches = CallRegex.Matches(trimmed);
            foreach (Match m in matches)
            {
                var objectName = m.Groups[1].Value;
                var calledFunc = m.Groups[2].Value;

                if (string.IsNullOrEmpty(calledFunc)) continue;
                if (_pythonBuiltins.Contains(calledFunc)) continue;
                if (_pythonBuiltins.Contains(objectName)) continue;
                if (calledFunc.StartsWith("_")) continue; // skip private
                if (char.IsUpper(calledFunc[0]) && string.IsNullOrEmpty(objectName)) continue; // class instantiation

                var calleeClass = string.IsNullOrEmpty(objectName)
                    ? currentClass
                    : InferClassName(objectName);

                if (calleeClass == currentClass && calledFunc == currentFunc) continue;

                result.CallGraphEdges.Add(new CallGraphEdge
                {
                    CallerClass = currentClass,
                    CallerMethod = currentFunc,
                    CalleeClass = calleeClass,
                    CalleeMethod = calledFunc,
                    Language = LanguageName,
                    CallerDisplayName = currentFuncDisplay,
                    CalleeDisplayName = calledFunc
                });
            }
        }
    }

    private static string ExtractFunctionBody(string[] lines, int funcLine, int funcIndent)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(lines[funcLine]);

        for (int i = funcLine + 1; i < lines.Length && i < funcLine + 150; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // If we hit a non-empty line with indent <= func indent, body is done
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                int indent = line.Length - trimmed.Length;
                if (indent <= funcIndent && !trimmed.StartsWith("#") && !trimmed.StartsWith("@"))
                    break;
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static string InferClassName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return "Unknown";
        // snake_case service names: user_service → UserService
        if (objectName.Contains('_'))
        {
            var parts = objectName.Split('_');
            return string.Concat(parts.Select(p => p.Length > 0 ? char.ToUpper(p[0]) + p[1..] : ""));
        }
        return char.ToUpper(objectName[0]) + objectName[1..];
    }
}
