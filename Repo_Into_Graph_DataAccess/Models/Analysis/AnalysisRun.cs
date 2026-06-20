

using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Models.Method;

namespace Repo_Into_Graph_DataAccess.Models.Analysis;

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
    public List<Business.Business> Businesses { get; set; } = new();
    public List<Feature.Feature> Features { get; set; } = new();
}



