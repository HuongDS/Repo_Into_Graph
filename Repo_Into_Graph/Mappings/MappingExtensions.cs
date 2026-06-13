using Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Dtos;

namespace Repo_Into_Graph.Repo_Into_Graph.Mappings
{
    public static class MappingExtensions
    {
        public static FeatureViewDto ToDto(this FeatureRecord record)
        {
            if (record == null) return null!;
            return new FeatureViewDto
            {
                Id = record.Id,
                AnalysisRunId = record.AnalysisRunId,
                FeatureName = record.FeatureName,
                CreatedAt = record.CreatedAt
            };
        }

        public static MethodSourceDto ToDto(this MethodSourceRecord record)
        {
            if (record == null) return null!;
            return new MethodSourceDto
            {
                Id = record.Id,
                ClassName = record.ClassName,
                MethodName = record.MethodName,
                SourceCode = record.SourceCode
            };
        }
    }
}
