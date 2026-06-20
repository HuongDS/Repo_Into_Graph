using Repo_Into_Graph_DataAccess.Models.Method;
using System;

namespace Repo_Into_Graph_DataAccess.Models.Feature
{
    public class FeatureMethodMapping
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
        public Guid MethodSourceId { get; set; }
        public DateTime MappedAt { get; set; }
        public Feature? Feature { get; set; }
        public MethodSourceRecord? MethodSource { get; set; }
    }
}
