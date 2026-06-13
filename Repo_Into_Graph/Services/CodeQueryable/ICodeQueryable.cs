using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Repo_Into_Graph.Repo_Into_Graph.Dtos;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.CodeQueryable
{
    public interface ICodeQueryable
    {
        Task<IEnumerable<FeatureViewDto>> GetMethodNamesAsync(Guid? id);
        Task<CodeFlowDto?> GetCodeFlowAsync(Guid featureId);
    }
}
