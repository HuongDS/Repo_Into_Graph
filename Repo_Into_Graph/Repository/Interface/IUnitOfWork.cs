using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Interface
{
    public interface IUnitOfWork : IAsyncDisposable, IDisposable
    {
        IAnalysisRunRepository AnalysisRuns { get; }
        ICallGraphEdgeRepository CallGraphEdges { get; }
        IMethodSourceRepository MethodSources { get; }
        IFeatureRepository Features { get; }
        IFeatureMethodMappingRepository FeatureMethodMappings { get; }
        IFewShotExampleRepository FewShotExamples { get; }

        Task<int> SaveChangesAsync();
    }
}
