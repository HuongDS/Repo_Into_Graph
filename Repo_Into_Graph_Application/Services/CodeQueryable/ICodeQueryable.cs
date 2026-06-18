using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.Code;
using Repo_Into_Graph_Application.Dtos.Feature;

namespace Repo_Into_Graph_Application.Services.CodeQueryable
{
    public interface ICodeQueryable
    {
        Task<IEnumerable<FeatureViewDto>> GetMethodNamesAsync(Guid? id);
        Task<CodeFlowDto?> GetCodeFlowAsync(Guid featureId);
    }
}





