using Repo_Into_Graph;
using Repo_Into_Graph.Services;

if (args.Length == 0)
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Static Code Analyzer - Call Graph & Data Flow Extractor    ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("Usage: Repo_Into_Graph <repository-path> [output-directory]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <repository-path>     Path to the directory containing C# source code");
    Console.WriteLine("  [output-directory]    Directory for output files (default: ./output)");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  Repo_Into_Graph C:\\MyProject ./analysis_output");
    Console.WriteLine();
    Console.WriteLine("Output files:");
    Console.WriteLine("  - output_graph.json       : Complete analysis data (Call Graph + Data Flow)");
    Console.WriteLine("  - call_graph.md           : Mermaid diagram of function calls");
    Console.WriteLine("  - data_flow_graph.md      : Mermaid diagram of data flow");
    return;
}

var repositoryPath = args[0];
var outputDir = args.Length > 1 ? args[1] : "./output";

if (!Directory.Exists(repositoryPath))
{
    Console.WriteLine($"❌ Error: Repository path does not exist: {repositoryPath}");
    return;
}

Directory.CreateDirectory(outputDir);

Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    Analyzing Repository...                    ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var analyzer = new CodeAnalyzer(repositoryPath);
var result = await analyzer.AnalyzeAsync();

Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                       Analysis Complete!                      ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"📊 Results Summary:");
Console.WriteLine($"   • Call Graph Edges: {result.CallGraph.Count}");
Console.WriteLine($"   • Data Flow Nodes:  {result.DataFlowGraph.Count}");
Console.WriteLine();

var outputJsonPath = Path.Combine(outputDir, "output_graph.json");
await OutputWriter.WriteJsonAsync(outputJsonPath, result);
await OutputWriter.WriteMermaidAsync(outputDir, result);
await OutputWriter.WriteHtmlAsync(outputDir, result);

Console.WriteLine();
Console.WriteLine("✅ Analysis complete! Check the output directory for results.");
Console.WriteLine();
Console.WriteLine("📁 Generated Files:");
Console.WriteLine($"   • output_graph.json       - Complete analysis data (JSON)");
Console.WriteLine($"   • call_graph.md           - Call graph (Markdown)");
Console.WriteLine($"   • data_flow_graph.md      - Data flow graph (Markdown)");
Console.WriteLine($"   • output_graph.html       - Combined visualization (HTML)");
Console.WriteLine($"   • call_graph.html         - Call graph only (HTML)");
Console.WriteLine($"   • data_flow_graph.html    - Data flow only (HTML)");
Console.WriteLine();
Console.WriteLine("💡 Tip: Open the HTML files in your browser for interactive visualization!");


