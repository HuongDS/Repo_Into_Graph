using Repo_Into_Graph_DataAccess.Models.Method;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class MethodSourceRepository : GenericRepository<MethodSourceRecord>, IMethodSourceRepository
    {
        public MethodSourceRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}




