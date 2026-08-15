using Repo_Into_Graph_DataAccess.Models.Method;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class MethodSourceRepository : GenericRepository<MethodSourceRecord>, IMethodSourceRepository
    {
        public MethodSourceRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<MethodSourceRecord>> GetByAnalysisRunIdAsync(Guid analysisRunId)
        {
            return await _dbSet
                .Where(m => m.AnalysisRunId == analysisRunId)
                .ToListAsync();
        }
    }
}




