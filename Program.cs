using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repo_Into_Graph;
using Repo_Into_Graph.Data;
using Repo_Into_Graph.Services;

string repositoryPath = string.Empty;
if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}
string outputDir = "./output";
if (args.Length > 0)
{
    repositoryPath = args[0];
    outputDir = args.Length > 1 ? args[1] : "./output";
}
else
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║            Static Code Analyzer - Call Graph Only              ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    while (string.IsNullOrWhiteSpace(repositoryPath))
    {
        Console.Write("👉 Nhập (hoặc nắm kéo thả) thư mục chứa code cần quét vào đây: ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("❌ Đường dẫn không được để trống!");
            continue;
        }
        repositoryPath = input.Trim('"', ' ');
    }

    Console.Write("📁 Nhập thư mục xuất kết quả (Bấm Enter để lấy mặc định './output'): ");
    string? inputDir = Console.ReadLine()?.Trim('"', ' ');
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
Console.WriteLine("║                    Analyzing Repository...                     ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var analyzer = new CodeAnalyzer(repositoryPath);
await using var dbContext = new AnalysisDbContext();
var databaseReady = false;

try
{
    await dbContext.Database.MigrateAsync();

    Console.WriteLine("✅ PostgreSQL schema ready via Migrations.");
    databaseReady = true;
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Cannot prepare PostgreSQL schema: {ex.Message}");
    Console.WriteLine("Check your Docker PostgreSQL container and configuration.");
}

var result = await analyzer.AnalyzeAsync();

Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                       Analysis Complete!                      ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("📊 Results Summary:");
Console.WriteLine($"   • Call Graph Edges: {result.CallGraph.Count}");
Console.WriteLine($"   • Method Sources:   {result.MethodSources.Count}");

// Language breakdown
var langBreakdown = result.CallGraph
    .Where(e => !string.IsNullOrEmpty(e.Language))
    .GroupBy(e => e.Language!)
    .OrderByDescending(g => g.Count());
if (langBreakdown.Any())
{
    Console.WriteLine("   • Language breakdown:");
    foreach (var g in langBreakdown)
        Console.WriteLine($"     - {g.Key}: {g.Count()} edge(s)");
}
Console.WriteLine();

try
{
    if (databaseReady)
    {
        var existingRuns = await dbContext.AnalysisRuns
            .Where(r => r.RepositoryPath.ToLower() == repositoryPath.ToLower())
            .ToListAsync();

        if (existingRuns.Any())
        {
            Console.WriteLine($"🗑️ Found existing analysis data for repository: {repositoryPath}");
            Console.WriteLine($"🗑️ Deleting {existingRuns.Count} old analysis run(s) and associated records...");
            dbContext.AnalysisRuns.RemoveRange(existingRuns);
            await dbContext.SaveChangesAsync();
            Console.WriteLine("🗑️ Old data deleted successfully.");
        }

        var analysisRun = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            RepositoryPath = repositoryPath,
            CreatedAt = DateTime.UtcNow,
            CallGraphEdges = result.CallGraph.Select(edge => new CallGraphEdgeRecord
            {
                Id = Guid.NewGuid(),
                CallerClass = edge.CallerClass,
                CallerMethod = edge.CallerMethod,
                CalleeClass = edge.CalleeClass,
                CalleeMethod = edge.CalleeMethod,
                CallerDisplayName = edge.CallerDisplayName,
                CalleeDisplayName = edge.CalleeDisplayName,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            MethodSources = result.MethodSources.Select(source => new MethodSourceRecord
            {
                Id = Guid.NewGuid(),
                ClassName = source.ClassName,
                MethodName = source.MethodName,
                SourceCode = source.SourceCode,
                HttpVerb = source.HttpVerb,
                DisplayName = source.DisplayName,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        await dbContext.AnalysisRuns.AddAsync(analysisRun);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✅ Saved {result.CallGraph.Count} call graph edges and {result.MethodSources.Count} method source codes to PostgreSQL.");

        var graphMapper = new GraphMapperService(dbContext);
        try
        {
            string featuresJsonPath = "./template_feature.json";

            await graphMapper.ProcessAndMapGraphAsync(analysisRun.Id, featuresJsonPath);

            Console.WriteLine("🚀 Successfully processed and mapped features from JSON to PostgreSQL!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during graph mapping: {ex.Message}");
        }

    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to save call graph to PostgreSQL: {ex.Message}");
}

var outputJsonPath = Path.Combine(outputDir, "output_graph.json");
await OutputWriter.WriteJsonAsync(outputJsonPath, result);
await OutputWriter.WriteMermaidAsync(outputDir, result);
await OutputWriter.WriteHtmlAsync(outputDir, result);

Console.WriteLine();
Console.WriteLine("✅ Analysis complete! Check the output directory for results.");
Console.WriteLine();
Console.WriteLine("📁 Generated Files:");
Console.WriteLine("   • output_graph.json       - Complete analysis data (JSON)");
Console.WriteLine("   • call_graph.md           - Call graph (Markdown)");
Console.WriteLine("   • output_graph.html       - Call graph visualization (HTML)");
Console.WriteLine("   • call_graph.html         - Call graph visualization (HTML)");
Console.WriteLine();
Console.WriteLine("💡 Tip: Open the HTML files in your browser for interactive visualization!");