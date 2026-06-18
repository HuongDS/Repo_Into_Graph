
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class AnalysisRunRepository : GenericRepository<AnalysisRun>, IAnalysisRunRepository
    {
        public AnalysisRunRepository(AnalysisDbContext context) : base(context)
        {
        }
    }
}




