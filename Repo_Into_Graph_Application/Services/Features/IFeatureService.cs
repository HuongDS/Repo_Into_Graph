using Repo_Into_Graph_Application.Dtos.Feature;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Features
{
    public interface IFeatureService
    {
        /// <summary>
        /// Lấy danh sách tất cả features có phân trang, lọc theo analysisRunId hoặc tên.
        /// </summary>
        Task<FeaturePagedResult> GetPagedAsync(
            int page,
            int pageSize,
            Guid? analysisRunId,
            string? name);

        /// <summary>
        /// Lấy toàn bộ features theo analysisRunId (không phân trang — dùng cho render toàn bộ graph).
        /// </summary>
        Task<IEnumerable<FeatureDetailDto>> GetAllByAnalysisRunAsync(Guid analysisRunId);

        /// <summary>
        /// Lấy chi tiết một feature theo ID, kèm steps và mermaid graphs.
        /// </summary>
        Task<FeatureDetailDto?> GetByIdAsync(Guid id);
    }
}
