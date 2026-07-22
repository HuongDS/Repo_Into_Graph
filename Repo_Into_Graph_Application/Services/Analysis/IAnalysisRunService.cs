using Repo_Into_Graph_Application.Dtos.Analysis;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Analysis
{
    public interface IAnalysisRunService
    {
        Task<PagedResult<AnalysisRunDto>> GetPagedAsync(
            int page,
            int pageSize,
            string? repoOwner,
            string? repoName,
            string? repoLanguage,
            bool? isPublic);

        Task<AnalysisRunDto?> GetByIdAsync(Guid id);

        Task<AnalysisRunDto> CreateAsync(CreateAnalysisRunRequest request);

        Task<AnalysisRunDto?> UpdateAsync(Guid id, UpdateAnalysisRunRequest request);
        Task<bool> DeleteAsync(Guid id);
    }
}





