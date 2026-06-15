using Repo_Into_Graph.Repo_Into_Graph.Models.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class FeatureMethodMappingRepository : GenericRepository<FeatureMethodMapping>, IFeatureMethodMappingRepository
    {
        public FeatureMethodMappingRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
