using System.Text;
using System.Text.Json;
using Repo_Into_Graph.Models;

namespace Repo_Into_Graph.Services;

public class OutputWriter
{
    public static async Task WriteJsonAsync(string outputPath, AnalysisResult result)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var outputObj = new
        {
            CallGraph = result.CallGraph
        };

        var json = JsonSerializer.Serialize(outputObj, options);
        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"✅ JSON output written to: {outputPath}");
    }

    public static async Task WriteMermaidAsync(string outputDir, AnalysisResult result)
    {
        if (string.IsNullOrEmpty(result.MermaidCallGraph))
        {
            return;
        }

        var callGraphPath = Path.Combine(outputDir, "call_graph.md");
        var callGraphContent = $"# Call Graph\n\n```mermaid\n{result.MermaidCallGraph}\n```";
        await File.WriteAllTextAsync(callGraphPath, callGraphContent);
        Console.WriteLine($"✅ Call graph written to: {callGraphPath}");
    }

    public static async Task WriteHtmlAsync(string outputDir, AnalysisResult result)
    {
        var htmlContent = BuildHtml(result);

        var outputGraphPath = Path.Combine(outputDir, "output_graph.html");
        await File.WriteAllTextAsync(outputGraphPath, htmlContent);
        Console.WriteLine($"✅ HTML visualization written to: {outputGraphPath}");

        var callGraphPath = Path.Combine(outputDir, "call_graph.html");
        await File.WriteAllTextAsync(callGraphPath, htmlContent);
        Console.WriteLine($"✅ Call graph HTML written to: {callGraphPath}");
    }

    private static string BuildHtml(AnalysisResult result)
    {
        var mermaidBlock = !string.IsNullOrEmpty(result.MermaidCallGraph)
            ? $"<div class=\"mermaid\">\n{result.MermaidCallGraph}\n</div>"
            : "<p style=\"color: #999;\">No call graph data available</p>";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Static Code Analysis - Call Graph</title>");
        sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js\"></script>");
        sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/panzoom@9.4.0/dist/panzoom.min.js\"></script>");
        sb.AppendLine("    <script>");
        sb.AppendLine("        mermaid.initialize({");
        sb.AppendLine("            startOnLoad: true,");
        sb.AppendLine("            maxEdges: 10000,");
        sb.AppendLine("            maxTextSize: 90000,");
        sb.AppendLine("            theme: 'default',");
        sb.AppendLine("            securityLevel: 'loose',");
        sb.AppendLine("            flowchart: {");
        sb.AppendLine("                rankSpacing: 100,");
        sb.AppendLine("                nodeSpacing: 150,");
        sb.AppendLine("                padding: 50");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        sb.AppendLine("    </script>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; padding: 12px; }");
        sb.AppendLine("        .container { max-width: 100%; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3); overflow: hidden; display: flex; flex-direction: column; height: calc(100vh - 24px); min-height: 0; }");
        sb.AppendLine("        .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px 40px; text-align: center; flex-shrink: 0; }");
        sb.AppendLine("        .header h1 { font-size: 24px; margin-bottom: 5px; font-weight: 700; }");
        sb.AppendLine("        .header p { font-size: 13px; opacity: 0.9; }");
        sb.AppendLine("        .content { flex: 1; overflow: auto; padding: 24px; min-height: 0; }");
        sb.AppendLine("        .mermaid { background: #fff; padding: 20px; border-radius: 8px; min-height: 500px; }");
        sb.AppendLine("        .mermaid svg { max-width: none !important; height: auto !important; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("    <script>");
        sb.AppendLine("        window.addEventListener('load', () => {");
        sb.AppendLine("            setTimeout(() => {");
        sb.AppendLine("                const svg = document.querySelector('.mermaid svg');");
        sb.AppendLine("                if (svg) {");
        sb.AppendLine("                    const panzoom = Panzoom(svg, { maxScale: 5, minScale: 0.1, contain: 'outside' });");
        sb.AppendLine("                    svg.parentElement.addEventListener('wheel', panzoom.zoomWithWheel);");
        sb.AppendLine("                }");
        sb.AppendLine("            }, 500);");
        sb.AppendLine("        });");
        sb.AppendLine("    </script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");
        sb.AppendLine("        <div class=\"header\">");
        sb.AppendLine("            <h1>Static Code Analysis Results</h1>");
        sb.AppendLine("            <p>Interactive call graph visualization</p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"content\">");
        sb.AppendLine(mermaidBlock);
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}