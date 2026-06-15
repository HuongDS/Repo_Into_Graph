
using Repo_Into_Graph.Repo_Into_Graph.Models.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class FeatureRepository : GenericRepository<FeatureRecord>, IFeatureRepository
    {
        public FeatureRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
