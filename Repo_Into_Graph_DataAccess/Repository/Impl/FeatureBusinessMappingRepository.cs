using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureBusinessMappingRepository : GenericRepository<FeatureBusinessMapping>, IFeatureBusinessMappingRepository
    {
        public FeatureBusinessMappingRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
