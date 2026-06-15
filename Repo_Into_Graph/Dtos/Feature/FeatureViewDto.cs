using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Dtos.Feature
{
    public class FeatureViewDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
