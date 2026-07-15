using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FewShotExampleRepository : GenericRepository<FewShotExample>, IFewShotExampleRepository
    {
        public FewShotExampleRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<FewShotExample>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            return await _dbSet
                .Where(e => ids.Contains(e.Id))
                .ToListAsync();
        }

        public async Task<List<FewShotExample>> GetByDifficultyAsync(string difficulty, int count = 5)
        {
            return await _dbSet
                .Where(e => e.Difficulty.ToLower() == difficulty.ToLower())
                .Take(count)
                .ToListAsync();
        }
    }
}




