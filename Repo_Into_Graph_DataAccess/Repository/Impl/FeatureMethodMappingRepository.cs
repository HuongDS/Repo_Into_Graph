using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureMethodMappingRepository : GenericRepository<FeatureMethodMapping>, IFeatureMethodMappingRepository
    {
        public FeatureMethodMappingRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<FeatureMethodMapping>> GetMappingsWithMethodSourceByFeatureIdsAsync(IEnumerable<Guid> featureIds)
        {
            return await _dbSet
                .Include(m => m.MethodSource)
                .Where(m => featureIds.Contains(m.FeatureId))
                .ToListAsync();
        }

        public async Task<List<Repo_Into_Graph_DataAccess.Models.Method.MethodSourceRecord>> GetMethodSourcesByFeatureIdsAsync(IEnumerable<Guid> featureIds)
        {
            return await _dbSet
                .Where(m => featureIds.Contains(m.FeatureId))
                .Select(m => m.MethodSource)
                .Where(ms => ms != null)
                .Distinct()
                .ToListAsync();
        }
    }
}
