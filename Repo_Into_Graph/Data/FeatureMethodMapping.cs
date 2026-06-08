using Repo_Into_Graph.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Data
{
    public class FeatureMethodMapping
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
        public Guid MethodSourceId { get; set; }
        public DateTime MappedAt { get; set; }
        public FeatureRecord? Feature { get; set; }
        public MethodSourceRecord? MethodSource { get; set; }
    }
}
