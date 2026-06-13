using Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class AnalysisRunRepository : GenericRepository<AnalysisRun>, IAnalysisRunRepository
    {
        public AnalysisRunRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
