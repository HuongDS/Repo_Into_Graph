using Repo_Into_Graph_DataAccess.Models.Feature;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IFeatureMethodMappingRepository : IGenericRepository<FeatureMethodMapping>
    {
        Task<List<FeatureMethodMapping>> GetMappingsWithMethodSourceByFeatureIdsAsync(IEnumerable<Guid> featureIds);
        Task<List<Repo_Into_Graph_DataAccess.Models.Method.MethodSourceRecord>> GetMethodSourcesByFeatureIdsAsync(IEnumerable<Guid> featureIds);
    }
}
