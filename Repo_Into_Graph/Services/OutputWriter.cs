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
        // Only include call graph + optional call graph mermaid string in JSON output.
        var outputObj = new
        {
            CallGraph = result.CallGraph,
            MermaidCallGraph = string.IsNullOrEmpty(result.MermaidCallGraph) ? null : result.MermaidCallGraph
        };

        var json = JsonSerializer.Serialize(outputObj, options);
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
            // If the call graph is large, split it per caller class to avoid Mermaid size/text limits
            var threshold = 350; // edges threshold to split by class
            if (result.CallGraph != null && result.CallGraph.Count > threshold)
            {
                var mg = new MermaidGenerator();
                var indexSb = new System.Text.StringBuilder();
                indexSb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Call Graph Index</title></head><body>");
                indexSb.AppendLine("<h1>Call Graph by Caller Class</h1><ul>");

                // Prefer splitting by controller: find classes that end with 'Controller'
                var controllers = result.CallGraph
                    .Select(e => e.CallerClass)
                    .Where(n => !string.IsNullOrEmpty(n) && n.EndsWith("Controller"))
                    .Distinct()
                    .ToList();

                // Fallback: if no controllers detected, group by caller class as before
                if (controllers.Count == 0)
                {
                    controllers = result.CallGraph
                        .Select(e => string.IsNullOrEmpty(e.CallerClass) ? "Unknown" : e.CallerClass)
                        .Distinct()
                        .ToList();
                }

                var safe = new Func<string, string>(s => string.Join('_', s.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_'));

                // Build a map from caller node (Class.Method) to outgoing edges for quick traversal
                var edgesByCaller = new Dictionary<string, List<CallGraphEdge>>();
                foreach (var edge in result.CallGraph)
                {
                    var callerKey = (edge.CallerClass ?? "") + "." + (edge.CallerMethod ?? "");
                    if (!edgesByCaller.TryGetValue(callerKey, out var list))
                    {
                        list = new List<CallGraphEdge>();
                        edgesByCaller[callerKey] = list;
                    }
                    list.Add(edge);
                }

                foreach (var controller in controllers)
                {
                    // Seed with all methods of the controller (class-level)
                    var seedNodes = result.CallGraph
                        .Where(e => string.Equals(e.CallerClass, controller, StringComparison.Ordinal))
                        .Select(e => (e.CallerClass ?? "") + "." + (e.CallerMethod ?? ""))
                        .Distinct()
                        .ToList();

                    var includedEdges = new List<CallGraphEdge>();
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var q = new Queue<string>(seedNodes);
                    foreach (var sn in seedNodes) visited.Add(sn);

                    while (q.Count > 0)
                    {
                        var node = q.Dequeue();
                        if (!edgesByCaller.TryGetValue(node, out var outs)) continue;
                        foreach (var e in outs)
                        {
                            includedEdges.Add(e);
                            var calleeNode = (e.CalleeClass ?? "") + "." + (e.CalleeMethod ?? "");
                            if (!visited.Contains(calleeNode))
                            {
                                visited.Add(calleeNode);
                                q.Enqueue(calleeNode);
                            }
                        }
                    }

                    // If nothing included (rare), skip
                    if (includedEdges.Count == 0) continue;

                    var mermaid = mg.GenerateCallGraph(includedEdges);
                    var fileName = $"call_graph_controller_{safe(controller)}.html";
                    var filePath = Path.Combine(outputDir, fileName);
                    var html = GenerateCallGraphHtml(mermaid);
                    await File.WriteAllTextAsync(filePath, html);
                    Console.WriteLine($"? Call graph (controller) written to: {filePath}");
                    indexSb.AppendLine($"<li><a href=\"{fileName}\">{System.Net.WebUtility.HtmlEncode(controller)}</a> ({includedEdges.Count} edges)</li>");
                }

                indexSb.AppendLine("</ul></body></html>");
                var indexPath = Path.Combine(outputDir, "call_graph_index.html");
                await File.WriteAllTextAsync(indexPath, indexSb.ToString());
                Console.WriteLine($"? Call graph index written to: {indexPath}");

                // Also write a small redirect root call_graph.html that links to the index
                var rootRedirect = $"<!doctype html><html><head><meta http-equiv=\"refresh\" content=\"0;url=call_graph_index.html\" /></head><body>Redirecting to <a href=\"call_graph_index.html\">index</a></body></html>";
                var rootPath = Path.Combine(outputDir, "call_graph.html");
                await File.WriteAllTextAsync(rootPath, rootRedirect);
            }
            else
            {
                var callGraphPath = Path.Combine(outputDir, "call_graph.html");
                var callGraphContent = GenerateCallGraphHtml(result.MermaidCallGraph);
                await File.WriteAllTextAsync(callGraphPath, callGraphContent);
                Console.WriteLine($"? Call graph HTML written to: {callGraphPath}");
            }
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
            ? $"<div class=\"mermaid\">\n{result.MermaidCallGraph}\n</div>"
            : "<p style=\"color: #999;\">No call graph data available</p>";

        var dataFlowContent = !string.IsNullOrEmpty(result.MermaidDataFlowGraph)
            ? $"<div class=\"mermaid\">\n{result.MermaidDataFlowGraph}\n</div>"
            : "<p style=\"color: #999;\">No data flow graph data available</p>";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Static Code Analysis - Call Graph & Data Flow</title>
    <script src=""https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/panzoom@9.4.0/dist/panzoom.min.js""></script>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            maxEdges: 10000,
            maxTextSize: 90000,
            theme: 'default',
            securityLevel: 'loose',
            flowchart: {{
                rankSpacing: 100,
                nodeSpacing: 150,
                padding: 50
            }}
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
            padding: 12px;
        }}

        .container {{
            max-width: 100%;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            overflow: hidden;
            display: flex;
            flex-direction: column;
            height: calc(100vh - 24px);
            min-height: 0;
        }}

        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px 40px;
            text-align: center;
            flex-shrink: 0;
        }}

        .header h1 {{
            font-size: 24px;
            margin-bottom: 5px;
            font-weight: 700;
        }}

        .header p {{
            font-size: 13px;
            opacity: 0.9;
        }}

        .tabs {{
            display: flex;
            border-bottom: 2px solid #e0e0e0;
            background: #f9f9f9;
            flex-shrink: 0;
        }}

        .tab {{
            flex: 1;
            padding: 12px 20px;
            text-align: center;
            cursor: pointer;
            border: none;
            background: none;
            font-size: 13px;
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
            padding: 24px 32px;
            flex: 1 1 auto;
            min-height: 0;
        }}

        .tab-content.active {{
            display: flex;
            flex-direction: column;
            gap: 16px;
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
            overflow: auto;
            min-height: 0;
            flex: 1 1 auto;
            resize: vertical;
            display: flex;
            align-items: stretch;
            justify-content: stretch;
            width: 100%;
        }}

        .graph-shell {{
            display: flex;
            flex-direction: column;
            gap: 12px;
            flex: 1 1 auto;
            min-height: 0;
        }}

        .controls {{
            background: #f9f9f9;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 12px 16px;
            display: flex;
            gap: 10px;
            align-items: center;
            flex-wrap: wrap;
        }}

        .control-btn {{
            padding: 8px 12px;
            background: #667eea;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
            transition: background 0.3s ease;
        }}

        .control-btn:hover {{
            background: #5568d3;
        }}

        .zoom-level {{
            font-size: 12px;
            color: #666;
            min-width: 60px;
        }}

        .graph-container > .mermaid,
        .graph-container pre.mermaid {{
            background: white;
            padding: 20px;
            border-radius: 6px;
            border: 1px solid #ddd;
            width: 100%;
            min-height: 100%;
        }}

        .graph-container > .mermaid {{
            display: flex;
            align-items: flex-start;
            justify-content: flex-start;
        }}

        .graph-container > .mermaid svg,
        .graph-container svg {{
            display: block;
            width: 100%;
            height: auto;
            max-width: none;
            max-height: none;
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
            <div class=""graph-shell"">
              
                <div class=""graph-container"" id=""callGraphContainer"">
                    {callGraphContent}
                </div>
            </div>
        </div>

        <div class=""tab-content"" id=""dataFlow"">
            <h2>Data Flow Graph - Variable Lifecycle Tracking</h2>
            <div class=""info-box"">
                <strong>?? Information:</strong> This diagram tracks how variables/objects flow through your system. 
                It shows where data originates (source), which methods it passes through, and where it ends (sink).
            </div>
            <div class=""graph-shell"">
                <div class=""controls"">
                    <button class=""control-btn"" onclick=""zoomIn('dataFlow')"">🔍+ Zoom In</button>
                    <button class=""control-btn"" onclick=""zoomOut('dataFlow')"">🔍- Zoom Out</button>
                    <button class=""control-btn"" onclick=""resetZoom('dataFlow')"">⟲ Reset</button>
                    <span class=""zoom-level"" id=""dataFlowZoomLevel"">100%</span>
                </div>
                <div class=""graph-container"" id=""dataFlowContainer"">
                    {dataFlowContent}
                </div>
            </div>
        </div>

        <div class=""footer"">
            <p>Generated by Static Code Analyzer (Phase 1) | Powered by Mermaid.js</p>
            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        const panzoomInstances = {{}};
        const graphState = {{}};

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
            event.currentTarget.classList.add('active');

            const container = document.getElementById(tabName + 'Container');
            if (container && !container.querySelector('svg') && window.mermaid) {{
                mermaid.contentLoaded();
            }}

            setTimeout(() => initializeGraph(tabName), 350);
        }}

        function initializeGraph(tabName) {{
            const container = document.getElementById(tabName + 'Container');
            if (!container) {{
                return;
            }}

            const svg = container.querySelector('svg');
            if (!svg) {{
                return;
            }}

            if (!graphState[tabName]) {{
                graphState[tabName] = {{
                    scale: 1,
                    x: 0,
                    y: 0,
                    svg: svg
                }};
            }} else {{
                graphState[tabName].svg = svg;
            }}

            svg.style.transformOrigin = '0 0';
            svg.style.maxWidth = 'none';
            svg.style.maxHeight = 'none';

            if (panzoomInstances[tabName]) {{
                updateZoomLevel(tabName);
                return;
            }}

            if (window.panzoom) {{
                svg.style.width = '100%';
                svg.style.height = '100%';
                panzoomInstances[tabName] = panzoom(svg, {{
                    minZoom: 0.1,
                    maxZoom: 6,
                    smoothScroll: false
                }});
            }}

            updateZoomLevel(tabName);
        }}

        function zoomIn(tabName) {{
            const instance = panzoomInstances[tabName];
            if (instance) {{
                instance.zoomBy(1.2, {{ animate: true }});
                updateZoomLevel(tabName);
                return;
            }}

            adjustFallbackZoom(tabName, 1.2);
        }}

        function zoomOut(tabName) {{
            const instance = panzoomInstances[tabName];
            if (instance) {{
                instance.zoomBy(0.8, {{ animate: true }});
                updateZoomLevel(tabName);
                return;
            }}

            adjustFallbackZoom(tabName, 0.8);
        }}

        function resetZoom(tabName) {{
            const instance = panzoomInstances[tabName];
            if (instance) {{
                instance.reset({{ animate: true }});
                updateZoomLevel(tabName);
                return;
            }}

            const state = graphState[tabName];
            if (state) {{
                state.scale = 1;
                state.x = 0;
                state.y = 0;
                applyFallbackTransform(tabName);
            }}
        }}

        function updateZoomLevel(tabName) {{
            const instance = panzoomInstances[tabName];
            const zoomLevelElement = document.getElementById(tabName + 'ZoomLevel');

            if (instance && zoomLevelElement) {{
                const transform = instance.getTransform();
                const zoom = Math.round(transform.scale * 100);
                zoomLevelElement.textContent = zoom + '%';
                return;
            }}

            const state = graphState[tabName];
            if (state && zoomLevelElement) {{
                const zoom = Math.round(state.scale * 100);
                zoomLevelElement.textContent = zoom + '%';
            }}
        }}

        function adjustFallbackZoom(tabName, multiplier) {{
            const state = graphState[tabName];
            if (!state) {{
                return;
            }}

            state.scale = Math.min(6, Math.max(0.1, state.scale * multiplier));
            applyFallbackTransform(tabName);
        }}

        function applyFallbackTransform(tabName) {{
            const state = graphState[tabName];
            if (!state || !state.svg) {{
                return;
            }}

            state.svg.style.transform = `translate(${{state.x}}px, ${{state.y}}px) scale(${{state.scale}})`;
            updateZoomLevel(tabName);
        }}

        // Ensure Mermaid renders on page load
        document.addEventListener('DOMContentLoaded', function() {{
            if (window.mermaid) {{
                mermaid.contentLoaded();
                setTimeout(() => initializeGraph('callGraph'), 300);
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
    <script src=""https://cdn.jsdelivr.net/npm/panzoom@9.4.0/dist/panzoom.min.js""></script>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            maxEdges: 10000,
            maxTextSize: 90000,
            theme: 'default',
            securityLevel: 'loose',
            flowchart: {{
                rankSpacing: 100,
                nodeSpacing: 150
            }}
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
            max-width: 100%;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            overflow: hidden;
            display: flex;
            flex-direction: column;
            height: 100vh;
        }}

        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px 40px;
            text-align: center;
            flex-shrink: 0;
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

        .controls {{
            background: #f9f9f9;
            border-bottom: 1px solid #e0e0e0;
            padding: 12px 20px;
            display: flex;
            gap: 10px;
            align-items: center;
            flex-shrink: 0;
        }}

        .control-btn {{
            padding: 8px 12px;
            background: #667eea;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
            transition: background 0.3s ease;
        }}

        .control-btn:hover {{
            background: #5568d3;
        }}

        .zoom-level {{
            font-size: 12px;
            color: #666;
            min-width: 60px;
        }}

        .info-box {{
            background: #e3f2fd;
            border-left: 4px solid #2196f3;
            padding: 10px 20px;
            font-size: 12px;
            color: #1565c0;
            flex-shrink: 0;
        }}

        .info-box strong {{
            color: #1565c0;
        }}

        .content {{
            flex: 1;
            overflow: hidden;
            position: relative;
        }}

        .graph-container {{
            width: 100%;
            height: 100%;
            overflow: auto;
            background: #fafafa;
        }}

        .mermaid {{
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100%;
            min-width: 100%;
        }}

        .mermaid svg {{
            max-width: none;
            max-height: none;
        }}

        .footer {{
            background: #f9f9f9;
            border-top: 1px solid #e0e0e0;
            padding: 12px 20px;
            text-align: center;
            font-size: 11px;
            color: #999;
            flex-shrink: 0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>📞 Call Graph Visualization</h1>
            <p>Method Invocation Relationships</p>
        </div>

        <div class=""controls"">
            <button class=""control-btn"" onclick=""zoomIn()"">🔍+ Zoom In</button>
            <button class=""control-btn"" onclick=""zoomOut()"">🔍- Zoom Out</button>
            <button class=""control-btn"" onclick=""resetZoom()"">⟲ Reset</button>
            <button class=""control-btn"" onclick=""fitToScreen()"">📦 Fit</button>
            <span class=""zoom-level"" id=""zoomLevel"">100%</span>
        </div>

        <div class=""info-box"">
            <strong>ℹ️ Information:</strong> This diagram shows which methods call which other methods. Use the controls above to zoom and pan the graph.
        </div>

        <div class=""content"">
            <div class=""graph-container"" id=""graphContainer"">
                <div class=""mermaid"">
{mermaidGraph}
                </div>
            </div>
        </div>

        <div class=""footer"">
            <p>Generated by Static Code Analyzer (Phase 1) | Powered by Mermaid.js</p>
            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        let panzoomInstance = null;

        async function initializeGraph() {{
            await new Promise(resolve => setTimeout(resolve, 500));
            
            const container = document.getElementById('graphContainer');
            const svg = container.querySelector('svg');
            
            if (svg) {{
                svg.style.width = '100%';
                svg.style.height = '100%';
                
                panzoomInstance = panzoom(svg, {{
                    minZoom: 0.1,
                    maxZoom: 5,
                    smoothScroll: false
                }});
                
                updateZoomLevel();
            }}
        }}

        function zoomIn() {{
            if (panzoomInstance) {{
                panzoomInstance.zoomBy(1.2, {{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function zoomOut() {{
            if (panzoomInstance) {{
                panzoomInstance.zoomBy(0.8, {{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function resetZoom() {{
            if (panzoomInstance) {{
                panzoomInstance.reset({{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function fitToScreen() {{
            if (panzoomInstance) {{
                panzoomInstance.zoomTo(0, 0, {{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function updateZoomLevel() {{
            if (panzoomInstance) {{
                const transform = panzoomInstance.getTransform();
                const zoom = Math.round(transform.scale * 100);
                document.getElementById('zoomLevel').textContent = zoom + '%';
            }}
        }}

        document.addEventListener('DOMContentLoaded', async () => {{
            if (window.mermaid) {{
                mermaid.contentLoaded();
                await initializeGraph();
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
    <script src=""https://cdn.jsdelivr.net/npm/panzoom@9.4.0/dist/panzoom.min.js""></script>
    <script>
        mermaid.initialize({{
            startOnLoad: true,
            maxEdges: 10000,
            maxTextSize: 90000,
            theme: 'default',
            securityLevel: 'loose',
            flowchart: {{
                rankSpacing: 100,
                nodeSpacing: 150
            }}
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
            max-width: 100%;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            overflow: hidden;
            display: flex;
            flex-direction: column;
            height: 100vh;
        }}

        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px 40px;
            text-align: center;
            flex-shrink: 0;
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

        .controls {{
            background: #f9f9f9;
            border-bottom: 1px solid #e0e0e0;
            padding: 12px 20px;
            display: flex;
            gap: 10px;
            align-items: center;
            flex-shrink: 0;
        }}

        .control-btn {{
            padding: 8px 12px;
            background: #667eea;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
            transition: background 0.3s ease;
        }}

        .control-btn:hover {{
            background: #5568d3;
        }}

        .zoom-level {{
            font-size: 12px;
            color: #666;
            min-width: 60px;
        }}

        .info-box {{
            background: #fce4ec;
            border-left: 4px solid #e91e63;
            padding: 10px 20px;
            font-size: 12px;
            color: #880e4f;
            flex-shrink: 0;
        }}

        .info-box strong {{
            color: #880e4f;
        }}

        .content {{
            flex: 1;
            overflow: hidden;
            position: relative;
        }}

        .graph-container {{
            width: 100%;
            height: 100%;
            overflow: auto;
            background: #fafafa;
        }}

        .mermaid {{
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100%;
            min-width: 100%;
        }}

        .mermaid svg {{
            max-width: none;
            max-height: none;
        }}

        .footer {{
            background: #f9f9f9;
            border-top: 1px solid #e0e0e0;
            padding: 12px 20px;
            text-align: center;
            font-size: 11px;
            color: #999;
            flex-shrink: 0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🔀 Data Flow Graph Visualization</h1>
            <p>Variable Lifecycle Tracking</p>
        </div>

        <div class=""controls"">
            <button class=""control-btn"" onclick=""zoomIn()"">🔍+ Zoom In</button>
            <button class=""control-btn"" onclick=""zoomOut()"">🔍- Zoom Out</button>
            <button class=""control-btn"" onclick=""resetZoom()"">⟲ Reset</button>
            <button class=""control-btn"" onclick=""fitToScreen()"">📦 Fit</button>
            <span class=""zoom-level"" id=""zoomLevel"">100%</span>
        </div>

        <div class=""info-box"">
            <strong>ℹ️ Information:</strong> This diagram tracks data flow through your system. Use the controls to zoom and navigate the graph.
        </div>

        <div class=""content"">
            <div class=""graph-container"" id=""graphContainer"">
                <div class=""mermaid"">
{mermaidGraph}
                </div>
            </div>
        </div>

        <div class=""footer"">
            <p>Generated by Static Code Analyzer (Phase 1) | Powered by Mermaid.js</p>
            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        let panzoomInstance = null;

        async function initializeGraph() {{
            await new Promise(resolve => setTimeout(resolve, 500));
            
            const container = document.getElementById('graphContainer');
            const svg = container.querySelector('svg');
            
            if (svg) {{
                svg.style.width = '100%';
                svg.style.height = '100%';
                
                panzoomInstance = panzoom(svg, {{
                    minZoom: 0.1,
                    maxZoom: 5,
                    smoothScroll: false
                }});
                
                updateZoomLevel();
            }}
        }}

        function zoomIn() {{
            if (panzoomInstance) {{
                panzoomInstance.zoomBy(1.2, {{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function zoomOut() {{
            if (panzoomInstance) {{
                panzoomInstance.zoomBy(0.8, {{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function resetZoom() {{
            if (panzoomInstance) {{
                panzoomInstance.reset({{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function fitToScreen() {{
            if (panzoomInstance) {{
                panzoomInstance.zoomTo(0, 0, {{ animate: true }});
                updateZoomLevel();
            }}
        }}

        function updateZoomLevel() {{
            if (panzoomInstance) {{
                const transform = panzoomInstance.getTransform();
                const zoom = Math.round(transform.scale * 100);
                document.getElementById('zoomLevel').textContent = zoom + '%';
            }}
        }}

        document.addEventListener('DOMContentLoaded', async () => {{
            if (window.mermaid) {{
                mermaid.contentLoaded();
                await initializeGraph();
            }}
        }});
    </script>
</body>
</html>";
    }
}
