using Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Models.Feature;
using System;

namespace Repo_Into_Graph.Repo_Into_Graph.Models.Method;

public class MethodSourceRecord
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }
    public List<FeatureMethodMapping> FeatureMethodMappings { get; set; } = new();
}
