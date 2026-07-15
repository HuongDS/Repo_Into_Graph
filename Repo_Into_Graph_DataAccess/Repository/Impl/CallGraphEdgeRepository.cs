
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class CallGraphEdgeRepository : GenericRepository<CallGraphEdge>, ICallGraphEdgeRepository
    {
        public CallGraphEdgeRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<CallGraphEdge>> GetByAnalysisRunIdAsync(Guid analysisRunId)
        {
            return await _dbSet
                .Where(e => e.AnalysisRunId == analysisRunId)
                .ToListAsync();
        }
    }
}




