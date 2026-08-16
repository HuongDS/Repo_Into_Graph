using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_Application.Services.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Models.Method;
using System.Text.Json;


namespace Repo_Into_Graph_Application.Services.Analysis;

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
        "target",
        // Thư mục static assets chuẩn của ASP.NET Core - chứa CSS/JS/ảnh phục vụ trình duyệt
        // (kể cả thư viện vendor copy tay như wwwroot/lib, wwwroot/adminlte...), không phải business
        // logic backend. Áp dụng chung cho mọi vị trí, không riêng repo có sub-project frontend tách biệt.
        "wwwroot"
    };

    // Dependency nào xuất hiện trong package.json thì coi thư mục chứa nó là 1 sub-project frontend
    // (UI framework thuần) và loại bỏ hoàn toàn khỏi phân tích - vì đây là repo full-stack, phần
    // logic nghiệp vụ cần trace nằm ở backend, code frontend chỉ gây nhiễu (component/UI, không phải
    // luồng nghiệp vụ; class/method trùng tên với backend có thể bị nối nhầm vào cùng 1 flow).
    private static readonly HashSet<string> _frontendFrameworkMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "react", "react-dom", "vue", "@vue/cli-service", "@angular/core", "svelte",
        "next", "nuxt", "gatsby", "vite", "@sveltejs/kit", "solid-js", "preact"
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

        var frontendRoots = DetectFrontendRoots();
        if (frontendRoots.Any())
        {
            Console.WriteLine($"🖥️  Detected {frontendRoots.Count} frontend sub-project(s) (React/Vue/Angular/...) - excluded from analysis:");
            foreach (var root in frontendRoots)
                Console.WriteLine($"   • {Path.GetRelativePath(_repositoryPath, root)}");
            Console.WriteLine();
        }

        var allFiles = Directory.GetFiles(_repositoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !IsInSkippedDirectory(f))
            .Where(f => !IsUnderAnyRoot(f, frontendRoots))
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

    // Quét mọi package.json trong repo (trừ những thư mục đã bị skip); thư mục nào có package.json
    // khai báo dependency của 1 UI framework thuần (React/Vue/Angular/...) được coi là root của 1
    // sub-project frontend riêng biệt và trả về để loại khỏi vùng phân tích.
    // Lưu ý: những framework full-stack như Next.js/Nuxt cũng bị coi là frontend theo cách này (đơn
    // giản hóa có chủ đích) - nếu route API viết trong cùng sub-project đó, phần đó cũng sẽ bị bỏ qua.
    private List<string> DetectFrontendRoots()
    {
        var roots = new List<string>();

        var packageJsonFiles = Directory.GetFiles(_repositoryPath, "package.json", SearchOption.AllDirectories)
            .Where(f => !IsInSkippedDirectory(f));

        foreach (var pkgFile in packageJsonFiles)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pkgFile));
                bool isFrontend = false;

                foreach (var section in new[] { "dependencies", "devDependencies" })
                {
                    if (!doc.RootElement.TryGetProperty(section, out var deps) || deps.ValueKind != JsonValueKind.Object)
                        continue;

                    if (deps.EnumerateObject().Any(p => _frontendFrameworkMarkers.Contains(p.Name)))
                    {
                        isFrontend = true;
                        break;
                    }
                }

                if (isFrontend)
                {
                    var dir = Path.GetDirectoryName(pkgFile);
                    if (!string.IsNullOrEmpty(dir))
                        roots.Add(dir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  Không đọc được {Path.GetFileName(pkgFile)}: {ex.Message}");
            }
        }

        return roots;
    }

    private static bool IsUnderAnyRoot(string filePath, List<string> roots)
    {
        if (roots.Count == 0) return false;

        var fullFile = Path.GetFullPath(filePath);
        foreach (var root in roots)
        {
            var fullRoot = Path.GetFullPath(root);
            if (fullFile.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                fullFile.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}




