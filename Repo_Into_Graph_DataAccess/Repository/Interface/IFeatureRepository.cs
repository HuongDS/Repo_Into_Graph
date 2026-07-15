using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IFeatureRepository : IGenericRepository<FeatureModel>
    {
        Task<List<FeatureModel>> GetFeaturesWithStepsByIdsAsync(IEnumerable<Guid> featureIds);
        Task<List<FeatureModel>> GetByAnalysisRunIdAsync(Guid analysisRunId);
    }
}
