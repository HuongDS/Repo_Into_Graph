using Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;
using System;
using System.Collections.Generic;

namespace Repo_Into_Graph.Repo_Into_Graph.Models.Feature;

public class FeatureRecord
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string FeatureName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public AnalysisRun? AnalysisRun { get; set; }

    public List<FeatureMethodMapping> FeatureMethodMappings { get; set; } = new();
}