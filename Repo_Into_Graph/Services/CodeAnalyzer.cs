using Repo_Into_Graph.Models;
using Repo_Into_Graph.Services.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace Repo_Into_Graph;

public class CodeAnalyzer
{
    private readonly string _repositoryPath;

    private readonly List<ILanguageParser> _parsers = new()
    {
        new CSharpParser(),
        new JavaParser(),
        new PythonParser(),
        new NodeJsParser()
    };

    private static readonly HashSet<string> _skipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin", "node_modules", ".git", ".github", ".vscode", ".idea",
        "__pycache__", ".pytest_cache", ".mypy_cache", "venv", ".venv", "env",
        "dist", "build", ".next", ".nuxt", "coverage", "migrations", "Migrations",
        "target"
    };

    public CodeAnalyzer(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
    }

    public async Task<AnalysisResult> AnalyzeAsync()
    {
        var allEdges = new List<CallGraphEdge>();
        var allSources = new List<MethodSource>();

        var extensionMap = new Dictionary<string, ILanguageParser>(StringComparer.OrdinalIgnoreCase);
        foreach (var parser in _parsers)
            foreach (var ext in parser.SupportedExtensions)
                extensionMap[ext] = parser;

        var allFiles = Directory.GetFiles(_repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !IsInSkippedDirectory(f))
            .Where(f => extensionMap.ContainsKey(Path.GetExtension(f)))
            .ToList();

        var csharpFiles = allFiles.Where(f => Path.GetExtension(f).Equals(".cs", StringComparison.OrdinalIgnoreCase)).ToList();
        var csharpTreesMap = new Dictionary<string, SyntaxTree>();

        foreach (var file in csharpFiles)
        {
            var code = await File.ReadAllTextAsync(file);
            csharpTreesMap[file] = CSharpSyntaxTree.ParseText(code);
        }
        var allCSharpTrees = csharpTreesMap.Values.ToList();
        // ------------------------------------------------------------------------

        var filesByLang = allFiles
            .GroupBy(f => extensionMap[Path.GetExtension(f)].LanguageName)
            .ToDictionary(g => g.Key, g => g.ToList());

        Console.WriteLine($"📂 Found {allFiles.Count} source files across {filesByLang.Count} language(s):");
        foreach (var (lang, files) in filesByLang)
            Console.WriteLine($"   • {lang}: {files.Count} file(s)");
        Console.WriteLine();

        int parsed = 0, errors = 0;
        foreach (var file in allFiles)
        {
            var ext = Path.GetExtension(file);
            var parser = extensionMap[ext];

            try
            {
                ExtractionResult extraction;

                if (parser is CSharpParser csParser && ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var targetTree = csharpTreesMap[file];
                    extraction = csParser.ParseWithFullContext(targetTree, allCSharpTrees, file);
                }
                else
                {
                    var code = await File.ReadAllTextAsync(file);
                    extraction = await parser.ParseAsync(file, code);
                }

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

        var uniqueEdges = allEdges
            .DistinctBy(e => $"{e.CallerClass}::{e.CallerMethod}→{e.CalleeClass}::{e.CalleeMethod}")
            .ToList();

        var mermaid = GenerateMermaid(uniqueEdges);

        return new AnalysisResult
        {
            CallGraph = uniqueEdges,
            MermaidCallGraph = mermaid,
            MethodSources = allSources
        };
    }

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

    private static bool IsInSkippedDirectory(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _skipDirs.Contains(part));
    }
}