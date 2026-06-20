using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureRepository : GenericRepository<FeatureModel>, IFeatureRepository
    {
        public FeatureRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
