
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FeatureRepository : GenericRepository<FeatureRecord>, IFeatureRepository
    {
        public FeatureRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}




