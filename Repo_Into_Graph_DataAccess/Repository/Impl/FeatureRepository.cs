using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureRepository : GenericRepository<FeatureModel>, IFeatureRepository
    {
        public FeatureRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<FeatureModel>> GetFeaturesWithStepsByIdsAsync(IEnumerable<Guid> featureIds)
        {
            return await _dbSet
                .Include(f => f.Steps)
                .Where(f => featureIds.Contains(f.Id))
                .ToListAsync();
        }

        public async Task<List<FeatureModel>> GetByAnalysisRunIdAsync(Guid analysisRunId)
        {
            return await _dbSet
                .Where(f => f.AnalysisRunId == analysisRunId)
                .ToListAsync();
        }
    }
}
