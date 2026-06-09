using Repo_Into_Graph.Models;
using Repo_Into_Graph.Services.Parsers;

namespace Repo_Into_Graph;

/// <summary>
/// Orchestrates multi-language static code analysis.
/// Automatically detects language by file extension and delegates to the
/// appropriate ILanguageParser implementation.
///
/// Supported languages:
///   - C# (.cs)         → CSharpParser  (Roslyn-based, full semantic analysis)
///   - Java (.java)     → JavaParser     (Spring Boot, regex/heuristic)
///   - Python (.py)     → PythonParser   (FastAPI, regex/heuristic)
///   - JS/TS (.js/.ts)  → NodeJsParser   (Express/NestJS, regex/heuristic)
/// </summary>
public class CodeAnalyzer
{
    private readonly string _repositoryPath;

    // ─── Registered parsers ─────────────────────────────────────────────────
    private readonly List<ILanguageParser> _parsers = new()
    {
        new CSharpParser(),
        new JavaParser(),
        new PythonParser(),
        new NodeJsParser()
    };

    // ─── Dirs to always skip ─────────────────────────────────────────────────
    private static readonly HashSet<string> _skipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin", "node_modules", ".git", ".github", ".vscode", ".idea",
        "__pycache__", ".pytest_cache", ".mypy_cache", "venv", ".venv", "env",
        "dist", "build", ".next", ".nuxt", "coverage", "migrations", "Migrations",
        "target" // Java/Maven build output
    };

    public CodeAnalyzer(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
    }

    public async Task<AnalysisResult> AnalyzeAsync()
    {
        var allEdges = new List<CallGraphEdge>();
        var allSources = new List<MethodSource>();

        // Build extension → parser lookup
        var extensionMap = new Dictionary<string, ILanguageParser>(StringComparer.OrdinalIgnoreCase);
        foreach (var parser in _parsers)
            foreach (var ext in parser.SupportedExtensions)
                extensionMap[ext] = parser;

        // Discover all files
        var allFiles = Directory.GetFiles(_repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !IsInSkippedDirectory(f))
            .Where(f => extensionMap.ContainsKey(Path.GetExtension(f)))
            .ToList();

        // Group by language for reporting
        var filesByLang = allFiles
            .GroupBy(f => extensionMap[Path.GetExtension(f)].LanguageName)
            .ToDictionary(g => g.Key, g => g.ToList());

        Console.WriteLine($"📂 Found {allFiles.Count} source files across {filesByLang.Count} language(s):");
        foreach (var (lang, files) in filesByLang)
            Console.WriteLine($"   • {lang}: {files.Count} file(s)");
        Console.WriteLine();

        // Parse each file with appropriate parser
        int parsed = 0, errors = 0;
        foreach (var file in allFiles)
        {
            var ext = Path.GetExtension(file);
            var parser = extensionMap[ext];

            try
            {
                var code = await File.ReadAllTextAsync(file);
                var extraction = await parser.ParseAsync(file, code);

                allEdges.AddRange(extraction.CallGraphEdges);
                allSources.AddRange(extraction.MethodSources);
                parsed++;

                Console.WriteLine($"  ✅ [{parser.LanguageName}] {Path.GetRelativePath(_repositoryPath, file)}");
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"  ❌ Error parsing {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"📊 Parse complete: {parsed} succeeded, {errors} failed.");
        Console.WriteLine($"   → {allEdges.Count} call graph edges");
        Console.WriteLine($"   → {allSources.Count} method sources");

        // Remove duplicate edges
        var uniqueEdges = allEdges
            .DistinctBy(e => $"{e.CallerClass}::{e.CallerMethod}→{e.CalleeClass}::{e.CalleeMethod}")
            .ToList();

        // Generate Mermaid diagram
        var mermaid = GenerateMermaid(uniqueEdges);

        return new AnalysisResult
        {
            CallGraph = uniqueEdges,
            MermaidCallGraph = mermaid,
            MethodSources = allSources
        };
    }

    // ─── Mermaid generation ──────────────────────────────────────────────────

    private static string GenerateMermaid(List<CallGraphEdge> edges)
    {
        if (edges.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("graph LR");

        var nodeIds = new Dictionary<string, string>();
        int nodeCounter = 0;

        string GetNodeId(string className, string methodName)
        {
            var key = $"{className}::{methodName}";
            if (!nodeIds.TryGetValue(key, out var id))
            {
                id = $"N{nodeCounter++}";
                nodeIds[key] = id;
                var label = SanitizeMermaid($"{className}.{methodName}");
                sb.AppendLine($"    {id}[\"{label}\"]");
            }
            return id;
        }

        foreach (var edge in edges)
        {
            var callerId = GetNodeId(edge.CallerClass, edge.CallerMethod);
            var calleeId = GetNodeId(edge.CalleeClass, edge.CalleeMethod);
            sb.AppendLine($"    {callerId} --> {calleeId}");
        }

        return sb.ToString();
    }

    private static string SanitizeMermaid(string text)
        => text.Replace("\"", "'").Replace("<", "&lt;").Replace(">", "&gt;");

    // ─── Directory filter ────────────────────────────────────────────────────

    private static bool IsInSkippedDirectory(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _skipDirs.Contains(part));
    }
}
