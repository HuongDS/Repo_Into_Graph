using Repo_Into_Graph.Models;
using System.Text;

namespace Repo_Into_Graph.Services;

public class MermaidGenerator
{
    public string GenerateCallGraph(List<CallGraphEdge> callGraph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        var uniqueEdges = new HashSet<string>();

        foreach (var edge in callGraph)
        {
            var nodeFrom = SanitizeNodeName($"{edge.CallerClass}_{edge.CallerMethod}");
            var nodeTo = SanitizeNodeName($"{edge.CalleeClass}_{edge.CalleeMethod}");
            var edgeKey = $"{nodeFrom} --> {nodeTo}";

            if (uniqueEdges.Add(edgeKey))
            {
                sb.AppendLine($"    {nodeFrom}[\"{edge.CallerClass}.{edge.CallerMethod}\"] --> {nodeTo}[\"{edge.CalleeClass}.{edge.CalleeMethod}\"]");
            }
        }

        return sb.ToString();
    }

    public string GenerateDataFlowGraph(List<DataFlowNode> dataFlowGraph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        foreach (var node in dataFlowGraph)
        {
            var varNodeId = SanitizeNodeName($"var_{node.VariableName}");
            var sourceNodeId = SanitizeNodeName($"source_{node.SourceLocation ?? "unknown"}");
            var sinkNodeId = SanitizeNodeName($"sink_{node.SinkLocation ?? "unknown"}");

            // Source
            if (!string.IsNullOrEmpty(node.SourceLocation))
            {
                sb.AppendLine($"    {sourceNodeId}[\"?? Source: {node.SourceLocation}\"]");
                sb.AppendLine($"    {sourceNodeId} -->|{node.VariableName} ({node.DataType})| {varNodeId}[\"{node.VariableName}\"]");
            }

            // Passed through methods
            if (node.PassedThroughMethods.Count > 0)
            {
                var methodsStr = string.Join(", ", node.PassedThroughMethods);
                sb.AppendLine($"    {varNodeId} -->|Passed Through| {SanitizeNodeName($"methods_{node.VariableName}")}[\"Methods: {methodsStr}\"]");
            }

            // Sink
            if (!string.IsNullOrEmpty(node.SinkLocation))
            {
                sb.AppendLine($"    {varNodeId} -->|{node.SinkType}| {sinkNodeId}[\"?? Sink: {node.SinkLocation}\"]");
            }
        }

        return sb.ToString();
    }

    private string SanitizeNodeName(string name)
    {
        return name
            .Replace(" ", "_")
            .Replace(".", "_")
            .Replace("-", "_")
            .Replace(":", "_")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(",", "")
            .Replace("/", "_");
    }
}
