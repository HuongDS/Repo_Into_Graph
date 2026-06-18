using Repo_Into_Graph_DataAccess.Models.BusinessFlows;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class BusinessFlowRepository : GenericRepository<BusinessFlow>, IBusinessFlowRepository
    {
        public BusinessFlowRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}




