using Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Models.Method;

namespace Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;

public class AnalysisResult
{
    public required List<CallGraphEdge> CallGraph { get; set; } = new();
    public string MermaidCallGraph { get; set; } = string.Empty;
    public List<MethodSource> MethodSources { get; set; } = new();
}
