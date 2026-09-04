using System;

namespace Repo_Into_Graph_Application.Dtos.Business
{
    public class BusinessViewDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
