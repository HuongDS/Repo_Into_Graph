using Repo_Into_Graph.Repo_Into_Graph.Dtos.BusinessFlow;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.BusinessFlows
{
    public interface IBusinessFlowService
    {
        /// <summary>
        /// Lấy danh sách tất cả business flows có phân trang, lọc theo analysisRunId hoặc tên.
        /// </summary>
        Task<BusinessFlowPagedResult> GetPagedAsync(
            int page,
            int pageSize,
            Guid? analysisRunId,
            string? name);

        /// <summary>
        /// Lấy toàn bộ business flows theo analysisRunId (không phân trang — dùng cho render toàn bộ graph).
        /// </summary>
        Task<IEnumerable<BusinessFlowDetailDto>> GetAllByAnalysisRunAsync(Guid analysisRunId);

        /// <summary>
        /// Lấy chi tiết một business flow theo ID, kèm steps và mermaid graphs.
        /// </summary>
        Task<BusinessFlowDetailDto?> GetByIdAsync(Guid id);
    }
}
