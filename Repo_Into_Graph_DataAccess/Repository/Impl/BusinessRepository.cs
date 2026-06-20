using BusinessModel = Repo_Into_Graph_DataAccess.Models.Business.Business;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class BusinessRepository : GenericRepository<BusinessModel>, IBusinessRepository
    {
        public BusinessRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}
