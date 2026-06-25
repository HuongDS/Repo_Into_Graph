using BusinessModel = Repo_Into_Graph_DataAccess.Models.Business.Business;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;
using Microsoft.EntityFrameworkCore;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class BusinessRepository : GenericRepository<BusinessModel>, IBusinessRepository
    {
        public BusinessRepository(AnalysisDbContext context) : base(context)
        {
        }

        public async Task<List<string>> GetAllMethod(Guid businessId)
        {
            var query = await _context.FeatureBusinessMappings
                .Where(fbm => fbm.BusinessId == businessId)
                .SelectMany(m=>m.Feature.Steps)
                .Select(s=>s.CalleeClass+"_"+s.CalleeMethod)
                .Distinct()
                .ToListAsync();

            return query;
        }
    }
}
