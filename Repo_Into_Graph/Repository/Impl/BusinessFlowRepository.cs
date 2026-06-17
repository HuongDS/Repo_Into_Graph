using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class BusinessFlowRepository : GenericRepository<BusinessFlow>, IBusinessFlowRepository
    {
        public BusinessFlowRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
