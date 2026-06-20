using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Repository.Interface;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureMethodMappingRepository : GenericRepository<FeatureMethodMapping>, IFeatureMethodMappingRepository
    {
        public FeatureMethodMappingRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
