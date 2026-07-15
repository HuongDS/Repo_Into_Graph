using Repo_Into_Graph_DataAccess.Models.Method;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IMethodSourceRepository : IGenericRepository<MethodSourceRecord>
    {
        Task<List<MethodSourceRecord>> GetByAnalysisRunIdAsync(Guid analysisRunId);
    }
}




