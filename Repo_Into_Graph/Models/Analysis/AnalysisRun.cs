

using Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;
using Repo_Into_Graph.Repo_Into_Graph.Models.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Models.Method;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;

public class AnalysisRun
{
    public Guid Id { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Repository metadata
    public string? RepoName { get; set; }
    public string? RepoOwner { get; set; }
    public string? RepoDescription { get; set; }
    public string? RepoUrl { get; set; }
    public string? RepoLanguage { get; set; }
    public int? RepoStars { get; set; }
    public bool? IsPublic { get; set; }
    public DateTime? RepoUpdatedAt { get; set; }

    public List<CallGraphEdge> CallGraphEdges { get; set; } = new();
    public List<MethodSourceRecord> MethodSources { get; set; } = new();
    public List<FeatureRecord> Features { get; set; } = new();
    public List<BusinessFlow> BusinessFlows { get; set; } = new();
}