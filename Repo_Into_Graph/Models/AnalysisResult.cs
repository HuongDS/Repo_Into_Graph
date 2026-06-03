namespace Repo_Into_Graph.Models;

public class AnalysisResult
{
    public required List<CallGraphEdge> CallGraph { get; set; } = new();
    public required List<DataFlowNode> DataFlowGraph { get; set; } = new();
    public string MermaidCallGraph { get; set; } = string.Empty;
    public string MermaidDataFlowGraph { get; set; } = string.Empty;
}
