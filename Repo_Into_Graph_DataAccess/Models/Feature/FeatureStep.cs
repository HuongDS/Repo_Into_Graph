using System;

namespace Repo_Into_Graph_DataAccess.Models.Feature
{
    public class FeatureStep
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
        public int StepOrder { get; set; }
        public string CallerClass { get; set; } = string.Empty;
        public string CallerMethod { get; set; } = string.Empty;
        public string CalleeClass { get; set; } = string.Empty;
        public string CalleeMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public Feature? Feature { get; set; }
    }
}
