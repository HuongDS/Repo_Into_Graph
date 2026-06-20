using Repo_Into_Graph_DataAccess.Models.Analysis;
using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_DataAccess.Models.Business;

public class Business
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public AnalysisRun? AnalysisRun { get; set; }

    public List<BusinessMethodMapping> BusinessMethodMappings { get; set; } = new();
    public List<FeatureBusinessMapping> FeatureBusinessMappings { get; set; } = new();
}
