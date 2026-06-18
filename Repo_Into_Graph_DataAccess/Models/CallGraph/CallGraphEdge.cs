using Repo_Into_Graph_DataAccess.Models.Analysis;

namespace Repo_Into_Graph_DataAccess.Models;

public class CallGraphEdge
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string CallerClass { get; set; } = string.Empty;
    public string CallerMethod { get; set; } = string.Empty;
    public string CalleeClass { get; set; } = string.Empty;
    public string CalleeMethod { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }
    public string? Language { get; set; }

}




