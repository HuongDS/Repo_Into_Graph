using BusinessModel = Repo_Into_Graph_DataAccess.Models.Business.Business;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IBusinessRepository : IGenericRepository<BusinessModel>
    {
        public Task<List<string>> GetAllMethod(Guid businessId);
    }
}
