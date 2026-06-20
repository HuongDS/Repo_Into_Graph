using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Models.Business;
using System;

namespace Repo_Into_Graph_DataAccess.Models.Method;

public class MethodSourceRecord
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }
    public List<Repo_Into_Graph_DataAccess.Models.Feature.FeatureMethodMapping> FeatureMethodMappings { get; set; } = new();
}








