using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class FewShotExampleRepository : GenericRepository<FewShotExample>, IFewShotExampleRepository
    {
        public FewShotExampleRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}




