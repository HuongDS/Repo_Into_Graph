namespace Repo_Into_Graph.Data;

public class AnalysisRun
{
    public Guid Id { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CallGraphEdgeRecord> CallGraphEdges { get; set; } = new();
    public List<MethodSourceRecord> MethodSources { get; set; } = new();
}