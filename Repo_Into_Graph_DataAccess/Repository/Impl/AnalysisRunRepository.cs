
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class AnalysisRunRepository : GenericRepository<AnalysisRun>, IAnalysisRunRepository
    {
        public AnalysisRunRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<(int TotalCount, List<AnalysisRun> Items)> GetPagedAnalysisRunsAsync(
            int page,
            int pageSize,
            string? repoOwner,
            string? repoName,
            string? repoLanguage,
            bool? isPublic)
        {
            IQueryable<AnalysisRun> query = _dbSet.OrderByDescending(x => x.CreatedAt);

            if (!string.IsNullOrWhiteSpace(repoOwner))
                query = query.Where(x => x.RepoOwner != null &&
                                         x.RepoOwner.ToLower().Contains(repoOwner.Trim().ToLower()));

            if (!string.IsNullOrWhiteSpace(repoName))
                query = query.Where(x => x.RepoName != null &&
                                         x.RepoName.ToLower().Contains(repoName.Trim().ToLower()));

            if (!string.IsNullOrWhiteSpace(repoLanguage))
                query = query.Where(x => x.RepoLanguage != null &&
                                         x.RepoLanguage.ToLower().Contains(repoLanguage.Trim().ToLower()));

            if (isPublic.HasValue)
                query = query.Where(x => x.IsPublic == isPublic.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (totalCount, items);
        }
    }
}




