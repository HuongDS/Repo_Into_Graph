using System;

namespace Repo_Into_Graph_DataAccess.Models.Analysis
{
    public class DataFlowEdge
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string SourceToken { get; set; } = string.Empty;
        public string TargetToken { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
