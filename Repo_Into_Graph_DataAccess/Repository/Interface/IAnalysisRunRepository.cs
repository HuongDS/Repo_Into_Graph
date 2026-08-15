using Repo_Into_Graph_DataAccess.Models.Analysis;
using System.Threading.Tasks;

using System.Collections.Generic;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IAnalysisRunRepository : IGenericRepository<AnalysisRun>
    {
        Task<(int TotalCount, List<AnalysisRun> Items)> GetPagedAnalysisRunsAsync(
            int page,
            int pageSize,
            string? repoOwner,
            string? repoName,
            string? repoLanguage,
            bool? isPublic);
    }
}




