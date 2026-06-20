using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Models.Business;
using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_DataAccess.Models.Feature
{
    public class Feature
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public string MermaidGraph { get; set; } = string.Empty;
        public string DataFlowMermaidGraph { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public AnalysisRun? AnalysisRun { get; set; }
        public List<FeatureStep> Steps { get; set; } = new();
        public List<FeatureBusinessMapping> FeatureBusinessMappings { get; set; } = new();
        public List<FeatureMethodMapping> FeatureMethodMappings { get; set; } = new();
    }
}
