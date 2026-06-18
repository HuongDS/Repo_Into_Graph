using System;

namespace Repo_Into_Graph_DataAccess.Models.BusinessFlows
{
    public class BusinessFlowStep
    {
        public Guid Id { get; set; }
        public Guid BusinessFlowId { get; set; }
        public string CallerClass { get; set; } = string.Empty;
        public string CallerMethod { get; set; } = string.Empty;
        public string CalleeClass { get; set; } = string.Empty;
        public string CalleeMethod { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public DateTime CreatedAt { get; set; }

        public BusinessFlow? BusinessFlow { get; set; }
    }
}




