using Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;
using System;
using System.Collections.Generic;

namespace Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows
{
    public class BusinessFlow
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public string MermaidGraph { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public AnalysisRun? AnalysisRun { get; set; }
        public List<BusinessFlowStep> Steps { get; set; } = new();
    }
}
