using Repo_Into_Graph.Repo_Into_Graph.Models.Method;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class MethodSourceRepository : GenericRepository<MethodSourceRecord>, IMethodSourceRepository
    {
        public MethodSourceRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
