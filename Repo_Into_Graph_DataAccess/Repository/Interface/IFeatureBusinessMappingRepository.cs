using Repo_Into_Graph_DataAccess.Models.Business;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IFeatureBusinessMappingRepository : IGenericRepository<FeatureBusinessMapping>
    {
        Task<List<Guid>> GetFeatureIdsByBusinessIdAsync(Guid businessId);
    }
}
