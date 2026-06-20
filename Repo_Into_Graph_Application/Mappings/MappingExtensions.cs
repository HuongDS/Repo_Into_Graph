using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Models.Method;
using Repo_Into_Graph_Application.Dtos.Business;
using Repo_Into_Graph_Application.Dtos.Method;

namespace Repo_Into_Graph_Application.Mappings
{
    public static class MappingExtensions
    {
        public static BusinessViewDto ToDto(this Business record)
        {
            if (record == null) return null!;
            return new BusinessViewDto
            {
                Id = record.Id,
                AnalysisRunId = record.AnalysisRunId,
                BusinessName = record.BusinessName,
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
