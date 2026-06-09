namespace Repo_Into_Graph.Models;

public class AnalysisResult
{
    public required List<CallGraphEdge> CallGraph { get; set; } = new();
    public string MermaidCallGraph { get; set; } = string.Empty;
    public List<MethodSource> MethodSources { get; set; } = new();
}
