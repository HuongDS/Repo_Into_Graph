
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class CallGraphEdgeRepository : GenericRepository<CallGraphEdge>, ICallGraphEdgeRepository
    {
        public CallGraphEdgeRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}




