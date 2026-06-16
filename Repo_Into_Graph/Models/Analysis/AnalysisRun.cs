using Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Models.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Models.Method;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;

namespace Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;

public class AnalysisRun
{
    public Guid Id { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CallGraphEdge> CallGraphEdges { get; set; } = new();
    public List<MethodSourceRecord> MethodSources { get; set; } = new();
    public List<FeatureRecord> Features { get; set; } = new();
    public List<BusinessFlow> BusinessFlows { get; set; } = new();
}