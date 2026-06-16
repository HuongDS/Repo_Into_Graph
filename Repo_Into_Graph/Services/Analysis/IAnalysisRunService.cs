using Repo_Into_Graph.Repo_Into_Graph.Dtos.Analysis;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.Analysis
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

        /// <summary>
        /// Trả về false nếu không tìm thấy, true nếu xóa thành công.
        /// </summary>
        Task<bool> DeleteAsync(Guid id);
    }
}
