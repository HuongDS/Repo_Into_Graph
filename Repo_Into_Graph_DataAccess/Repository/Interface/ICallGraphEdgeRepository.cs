using Repo_Into_Graph_DataAccess.Models;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface ICallGraphEdgeRepository : IGenericRepository<CallGraphEdge>
    {
        Task<List<CallGraphEdge>> GetByAnalysisRunIdAsync(Guid analysisRunId);
    }
}




