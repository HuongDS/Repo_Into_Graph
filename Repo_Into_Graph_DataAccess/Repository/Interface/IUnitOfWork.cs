using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IUnitOfWork : IAsyncDisposable, IDisposable
    {
        IAnalysisRunRepository AnalysisRuns { get; }
        ICallGraphEdgeRepository CallGraphEdges { get; }
        IMethodSourceRepository MethodSources { get; }
        IFeatureRepository Features { get; }
        IFeatureMethodMappingRepository FeatureMethodMappings { get; }
        IFewShotExampleRepository FewShotExamples { get; }
        IBusinessFlowRepository BusinessFlows { get; }

        Task<int> SaveChangesAsync();
    }
}




