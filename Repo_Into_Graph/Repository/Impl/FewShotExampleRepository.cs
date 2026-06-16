using Repo_Into_Graph.Repo_Into_Graph.Models.FewShot;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class FewShotExampleRepository : GenericRepository<FewShotExample>, IFewShotExampleRepository
    {
        public FewShotExampleRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
