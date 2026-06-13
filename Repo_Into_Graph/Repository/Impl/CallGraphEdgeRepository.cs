using Repo_Into_Graph.Data;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class CallGraphEdgeRepository : GenericRepository<CallGraphEdgeRecord>, ICallGraphEdgeRepository
    {
        public CallGraphEdgeRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
