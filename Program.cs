using Repo_Into_Graph;
using Repo_Into_Graph.Services;



string repositoryPath = "";
string outputDir = "./output";

if (args.Length > 0)
{
    repositoryPath = args[0];
    outputDir = args.Length > 1 ? args[1] : "./output";
}
else
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║    Static Code Analyzer - Call Graph & Data Flow Extractor     ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    while (string.IsNullOrWhiteSpace(repositoryPath))
    {
        Console.Write($"👉 Nhập (hoặc nắm kéo thả) thư mục chứa code cần quét vào đây: ");
        string input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("❌ Đường dẫn không được để trống!");
            continue;
        }

        repositoryPath = input.Trim('"', ' ');
    }

    Console.Write("📁 Nhập thư mục xuất kết quả (Bấm Enter để lấy mặc định './output'): ");
    string inputDir = Console.ReadLine()?.Trim('"', ' ');
    if (!string.IsNullOrWhiteSpace(inputDir))
    {
        outputDir = inputDir;
    }
}

if (!Directory.Exists(repositoryPath))
{
    Console.WriteLine($"❌ Thư mục không tồn tại: {repositoryPath}");
    Console.WriteLine("Bấm phím bất kỳ để thoát...");
    Console.ReadKey();
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

Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              Generating Architecture Graph...                 ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();






