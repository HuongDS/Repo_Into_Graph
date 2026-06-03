using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Repo_Into_Graph.Models;
using Repo_Into_Graph.Services;

namespace Repo_Into_Graph;

public class CodeAnalyzer
{
    private readonly string _repositoryPath;
    private readonly CallGraphExtractor _callGraphExtractor = new();
    private readonly DataFlowGraphExtractor _dataFlowExtractor = new();
    private readonly MermaidGenerator _mermaidGenerator = new();
    private readonly List<CallGraphEdge> _allCallGraphEdges = new();
    private readonly List<DataFlowNode> _allDataFlowNodes = new();

    public CodeAnalyzer(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
    }

    public async Task<AnalysisResult> AnalyzeAsync()
    {
        var csharpFiles = Directory.GetFiles(_repositoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj\\") && !f.Contains("bin\\"))
            .ToList();

        Console.WriteLine($"Found {csharpFiles.Count} C# files to analyze.");

        if (csharpFiles.Count == 0)
        {
            return new AnalysisResult
            {
                CallGraph = new(),
                DataFlowGraph = new()
            };
        }

        var syntaxTrees = new List<SyntaxTree>();
        var fileMap = new Dictionary<SyntaxTree, string>();

        // Parse all files
        foreach (var file in csharpFiles)
        {
            try
            {
                var code = await File.ReadAllTextAsync(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                syntaxTrees.Add(tree);
                fileMap[tree] = file;
                Console.WriteLine($"? Parsed: {file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error parsing {file}: {ex.Message}");
            }
        }

        // Create compilation
        var compilation = CSharpCompilation.Create("TempAnalysis")
            .AddSyntaxTrees(syntaxTrees)
            .AddReferences(ReferenceAssemblies());

        // Extract call graph and data flow
        foreach (var tree in syntaxTrees)
        {
            var filePath = fileMap[tree];
            var semanticModel = compilation.GetSemanticModel(tree);

            try
            {
                var callGraphEdges = _callGraphExtractor.Extract(filePath, tree, semanticModel);
                _allCallGraphEdges.AddRange(callGraphEdges);

                var dataFlowNodes = _dataFlowExtractor.Extract(filePath, tree, semanticModel);
                _allDataFlowNodes.AddRange(dataFlowNodes);

                Console.WriteLine($"? Analyzed: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error analyzing {filePath}: {ex.Message}");
            }
        }

        // Generate Mermaid diagrams
        var mermaidCallGraph = _mermaidGenerator.GenerateCallGraph(_allCallGraphEdges);
        var mermaidDataFlow = _mermaidGenerator.GenerateDataFlowGraph(_allDataFlowNodes);

        return new AnalysisResult
        {
            CallGraph = _allCallGraphEdges,
            DataFlowGraph = _allDataFlowNodes,
            MermaidCallGraph = mermaidCallGraph,
            MermaidDataFlowGraph = mermaidDataFlow
        };
    }

    private List<MetadataReference> ReferenceAssemblies()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
        var coreLibPath = Path.Combine(runtimePath, "System.Runtime.dll");

        if (File.Exists(coreLibPath))
        {
            references.Add(MetadataReference.CreateFromFile(coreLibPath));
        }

        return references;
    }
}
