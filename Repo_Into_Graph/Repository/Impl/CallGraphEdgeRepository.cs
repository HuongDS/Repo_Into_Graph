
using global::Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class CallGraphEdgeRepository : GenericRepository<CallGraphEdge>, ICallGraphEdgeRepository
    {
        public CallGraphEdgeRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
