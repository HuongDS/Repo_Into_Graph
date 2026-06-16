using System;

namespace Repo_Into_Graph.Repo_Into_Graph.Dtos.Analysis
{
    public class AnalysisResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public Guid AnalysisRunId { get; set; }
        public int EdgesCount { get; set; }
        public int MethodsCount { get; set; }
    }
}
