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

        var json = JsonSerializer.Serialize(result, options);
        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"? JSON output written to: {outputPath}");
    }

    public static async Task WriteMermaidAsync(string outputDir, AnalysisResult result)
    {
        // Write call graph
        if (!string.IsNullOrEmpty(result.MermaidCallGraph))
        {
            var callGraphPath = Path.Combine(outputDir, "call_graph.md");
            var callGraphContent = $"# Call Graph\n\n```mermaid\n{result.MermaidCallGraph}\n```";
            await File.WriteAllTextAsync(callGraphPath, callGraphContent);
            Console.WriteLine($"? Call graph written to: {callGraphPath}");
        }

        // Write data flow graph
        if (!string.IsNullOrEmpty(result.MermaidDataFlowGraph))
        {
            var dataFlowPath = Path.Combine(outputDir, "data_flow_graph.md");
            var dataFlowContent = $"# Data Flow Graph\n\n```mermaid\n{result.MermaidDataFlowGraph}\n```";
            await File.WriteAllTextAsync(dataFlowPath, dataFlowContent);
            Console.WriteLine($"? Data flow graph written to: {dataFlowPath}");
        }
    }

    public static async Task WriteHtmlAsync(string outputDir, AnalysisResult result)
    {
        // Generate combined HTML with both graphs
        var htmlContent = GenerateHtmlContent(result);
        var htmlPath = Path.Combine(outputDir, "output_graph.html");
        await File.WriteAllTextAsync(htmlPath, htmlContent);
        Console.WriteLine($"? HTML visualization written to: {htmlPath}");

        // Also generate separate HTML files for convenience
        if (!string.IsNullOrEmpty(result.MermaidCallGraph))
        {
            var callGraphHtml = GenerateCallGraphHtml(result.MermaidCallGraph);
            var callGraphPath = Path.Combine(outputDir, "call_graph.html");
            await File.WriteAllTextAsync(callGraphPath, callGraphHtml);
            Console.WriteLine($"? Call graph HTML written to: {callGraphPath}");
        }

        if (!string.IsNullOrEmpty(result.MermaidDataFlowGraph))
        {
            var dataFlowHtml = GenerateDataFlowGraphHtml(result.MermaidDataFlowGraph);
            var dataFlowPath = Path.Combine(outputDir, "data_flow_graph.html");
            await File.WriteAllTextAsync(dataFlowPath, dataFlowHtml);
            Console.WriteLine($"? Data flow graph HTML written to: {dataFlowPath}");
        }
    }

    private static string GenerateHtmlContent(AnalysisResult result)
    {
        var callGraphContent = !string.IsNullOrEmpty(result.MermaidCallGraph)
            ? $"<pre class=\"mermaid\">\n{result.MermaidCallGraph}\n</pre>"
            : "<p style=\"color: #999;\">No call graph data available</p>";

        var dataFlowContent = !string.IsNullOrEmpty(result.MermaidDataFlowGraph)
            ? $"<pre class=\"mermaid\">\n{result.MermaidDataFlowGraph}\n</pre>"
            : "<p style=\"color: #999;\">No data flow graph data available</p>";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Static Code Analysis - Call Graph & Data Flow</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js""></script>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            maxEdges: 5000,
            theme: 'default',
            securityLevel: 'loose'
        }});
    </script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }}

        .container {{
            max-width: 1400px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            overflow: hidden;
        }}

        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px 40px;
            text-align: center;
        }}

        .header h1 {{
            font-size: 28px;
            margin-bottom: 10px;
            font-weight: 700;
        }}

        .header p {{
            font-size: 14px;
            opacity: 0.9;
        }}

        .tabs {{
            display: flex;
            border-bottom: 2px solid #e0e0e0;
            background: #f9f9f9;
        }}

        .tab {{
            flex: 1;
            padding: 15px 20px;
            text-align: center;
            cursor: pointer;
            border: none;
            background: none;
            font-size: 14px;
            font-weight: 600;
            color: #666;
            transition: all 0.3s ease;
            border-bottom: 3px solid transparent;
            position: relative;
            bottom: -2px;
        }}

        .tab:hover {{
            color: #667eea;
            background: #f0f0f0;
        }}

        .tab.active {{
            color: #667eea;
            border-bottom-color: #667eea;
            background: white;
        }}

        .tab-content {{
            display: none;
            padding: 30px 40px;
        }}

        .tab-content.active {{
            display: block;
        }}

        .tab-content h2 {{
            color: #333;
            font-size: 20px;
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
        }}

        .tab-content h2::before {{
            content: '';
            display: inline-block;
            width: 4px;
            height: 24px;
            background: #667eea;
            border-radius: 2px;
        }}

        .graph-container {{
            background: #f9f9f9;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 20px;
            overflow-x: auto;
            min-height: 400px;
            display: flex;
            align-items: center;
            justify-content: center;
        }}

        .graph-container pre.mermaid {{
            background: white;
            padding: 20px;
            border-radius: 6px;
            border: 1px solid #ddd;
            width: 100%;
            min-height: 400px;
        }}

        .info-box {{
            background: #e3f2fd;
            border-left: 4px solid #2196f3;
            padding: 15px 20px;
            border-radius: 4px;
            margin-bottom: 20px;
            font-size: 13px;
            color: #1565c0;
        }}

        .info-box strong {{
            color: #1565c0;
        }}

        .footer {{
            background: #f9f9f9;
            border-top: 1px solid #e0e0e0;
            padding: 20px 40px;
            text-align: center;
            font-size: 12px;
            color: #999;
        }}

        @media (max-width: 768px) {{
            .header {{
                padding: 20px;
            }}

            .header h1 {{
                font-size: 22px;
            }}

            .tabs {{
                flex-wrap: wrap;
            }}

            .tab {{
                flex: 1 1 50%;
                min-width: 150px;
            }}

            .tab-content {{
                padding: 20px;
            }}

            .container {{
                border-radius: 8px;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>?? Static Code Analysis Report</h1>
            <p>Call Graph & Data Flow Visualization</p>
        </div>

        <div class=""tabs"">
            <button class=""tab active"" onclick=""switchTab(event, 'callGraph')"">
                ?? Call Graph
            </button>
            <button class=""tab"" onclick=""switchTab(event, 'dataFlow')"">
                ?? Data Flow Graph
            </button>
        </div>

        <div class=""tab-content active"" id=""callGraph"">
            <h2>Call Graph - Method Invocation Relationships</h2>
            <div class=""info-box"">
                <strong>?? Information:</strong> This diagram shows which methods call which other methods. 
                Nodes represent methods, and edges show invocation relationships.
            </div>
            <div class=""graph-container"">
                {callGraphContent}
            </div>
        </div>

        <div class=""tab-content"" id=""dataFlow"">
            <h2>Data Flow Graph - Variable Lifecycle Tracking</h2>
            <div class=""info-box"">
                <strong>?? Information:</strong> This diagram tracks how variables/objects flow through your system. 
                It shows where data originates (source), which methods it passes through, and where it ends (sink).
            </div>
            <div class=""graph-container"">
                {dataFlowContent}
            </div>
        </div>

        <div class=""footer"">
            <p>Generated by Static Code Analyzer (Phase 1) | Powered by Mermaid.js</p>
            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        function switchTab(event, tabName) {{
            // Hide all tab contents
            const tabContents = document.querySelectorAll('.tab-content');
            tabContents.forEach(content => content.classList.remove('active'));

            // Remove active class from all tabs
            const tabs = document.querySelectorAll('.tab');
            tabs.forEach(tab => tab.classList.remove('active'));

            // Show selected tab content
            document.getElementById(tabName).classList.add('active');

            // Add active class to clicked tab
            event.target.classList.add('active');
        }}

        // Ensure Mermaid renders on page load
        document.addEventListener('DOMContentLoaded', function() {{
            if (window.mermaid) {{
                mermaid.contentLoaded();
            }}
        }});
    </script>
</body>
</html>";
    }

    private static string GenerateCallGraphHtml(string mermaidGraph)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Call Graph - Static Code Analysis</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js""></script>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            maxEdges: 5000,
            theme: 'default',
            securityLevel: 'loose'
        }});
    </script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }}

        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            overflow: hidden;
        }}

        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px 40px;
            text-align: center;
        }}

        .header h1 {{
            font-size: 28px;
            margin-bottom: 5px;
            font-weight: 700;
        }}

        .header p {{
            font-size: 14px;
            opacity: 0.9;
        }}

        .content {{
            padding: 40px;
        }}

        .content h2 {{
            color: #333;
            font-size: 20px;
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
        }}

        .content h2::before {{
            content: '';
            display: inline-block;
            width: 4px;
            height: 24px;
            background: #667eea;
            border-radius: 2px;
        }}

        .graph-container {{
            background: #f9f9f9;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 20px;
            overflow-x: auto;
            min-height: 500px;
            display: flex;
            align-items: center;
            justify-content: center;
        }}

        .graph-container pre.mermaid {{
            background: white;
            padding: 20px;
            border-radius: 6px;
            border: 1px solid #ddd;
            width: 100%;
            min-height: 500px;
        }}

        .info-box {{
            background: #e3f2fd;
            border-left: 4px solid #2196f3;
            padding: 15px 20px;
            border-radius: 4px;
            margin-bottom: 20px;
            font-size: 13px;
            color: #1565c0;
        }}

        .footer {{
            background: #f9f9f9;
            border-top: 1px solid #e0e0e0;
            padding: 20px 40px;
            text-align: center;
            font-size: 12px;
            color: #999;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>?? Call Graph Visualization</h1>
            <p>Method Invocation Relationships</p>
        </div>

        <div class=""content"">
            <h2>Call Graph - Method Invocations</h2>
            <div class=""info-box"">
                <strong>?? Information:</strong> This diagram shows which methods call which other methods. 
                Nodes represent methods, and arrows show the direction of method calls.
            </div>
            <div class=""graph-container"">
                <pre class=""mermaid"">
{mermaidGraph}
                </pre>
            </div>
        </div>

        <div class=""footer"">
            <p>Generated by Static Code Analyzer (Phase 1) | Powered by Mermaid.js</p>
            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        document.addEventListener('DOMContentLoaded', function() {{
            if (window.mermaid) {{
                mermaid.contentLoaded();
            }}
        }});
    </script>
</body>
</html>";
    }

    private static string GenerateDataFlowGraphHtml(string mermaidGraph)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Data Flow Graph - Static Code Analysis</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js""></script>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            maxEdges: 5000,
            theme: 'default',
            securityLevel: 'loose'
        }});
    </script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }}

        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            overflow: hidden;
        }}

        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px 40px;
            text-align: center;
        }}

        .header h1 {{
            font-size: 28px;
            margin-bottom: 5px;
            font-weight: 700;
        }}

        .header p {{
            font-size: 14px;
            opacity: 0.9;
        }}

        .content {{
            padding: 40px;
        }}

        .content h2 {{
            color: #333;
            font-size: 20px;
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
        }}

        .content h2::before {{
            content: '';
            display: inline-block;
            width: 4px;
            height: 24px;
            background: #667eea;
            border-radius: 2px;
        }}

        .graph-container {{
            background: #f9f9f9;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 20px;
            overflow-x: auto;
            min-height: 500px;
            display: flex;
            align-items: center;
            justify-content: center;
        }}

        .graph-container pre.mermaid {{
            background: white;
            padding: 20px;
            border-radius: 6px;
            border: 1px solid #ddd;
            width: 100%;
            min-height: 500px;
        }}

        .info-box {{
            background: #fce4ec;
            border-left: 4px solid #e91e63;
            padding: 15px 20px;
            border-radius: 4px;
            margin-bottom: 20px;
            font-size: 13px;
            color: #880e4f;
        }}

        .footer {{
            background: #f9f9f9;
            border-top: 1px solid #e0e0e0;
            padding: 20px 40px;
            text-align: center;
            font-size: 12px;
            color: #999;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>?? Data Flow Graph Visualization</h1>
            <p>Variable Lifecycle Tracking</p>
        </div>

        <div class=""content"">
            <h2>Data Flow Graph - Variable Transformations</h2>
            <div class=""info-box"">
                <strong>?? Information:</strong> This diagram tracks how data/variables flow through your system. 
                It shows source (where data originates), transformations (methods it passes through), and sink (where it ends).
            </div>
            <div class=""graph-container"">
                <pre class=""mermaid"">
{mermaidGraph}
                </pre>
            </div>
        </div>

        <div class=""footer"">
            <p>Generated by Static Code Analyzer (Phase 1) | Powered by Mermaid.js</p>
            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        document.addEventListener('DOMContentLoaded', function() {{
            if (window.mermaid) {{
                mermaid.contentLoaded();
            }}
        }});
    </script>
</body>
</html>";
    }
}
