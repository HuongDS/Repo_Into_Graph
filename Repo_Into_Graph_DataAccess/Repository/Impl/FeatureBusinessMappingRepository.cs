using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureBusinessMappingRepository : GenericRepository<FeatureBusinessMapping>, IFeatureBusinessMappingRepository
    {
        public FeatureBusinessMappingRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<Guid>> GetFeatureIdsByBusinessIdAsync(Guid businessId)
        {
            return await _dbSet
                .Where(m => m.BusinessId == businessId)
                .Select(m => m.FeatureId)
                .ToListAsync();
        }
    }
}
