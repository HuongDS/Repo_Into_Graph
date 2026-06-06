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
