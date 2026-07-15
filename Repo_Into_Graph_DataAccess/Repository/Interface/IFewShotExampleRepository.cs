using Repo_Into_Graph_DataAccess.Models.FewShot;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IFewShotExampleRepository : IGenericRepository<FewShotExample>
    {
        Task<List<FewShotExample>> GetByIdsAsync(IEnumerable<Guid> ids);
        Task<List<FewShotExample>> GetByDifficultyAsync(string difficulty, int count = 5);
    }
}




