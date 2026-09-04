using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IUnitOfWork : IAsyncDisposable, IDisposable
    {
        IAnalysisRunRepository AnalysisRuns { get; }
        ICallGraphEdgeRepository CallGraphEdges { get; }
        IMethodSourceRepository MethodSources { get; }
        IBusinessRepository Businesses { get; }
        IFeatureMethodMappingRepository FeatureMethodMappings { get; }
        IFeatureRepository Features { get; }
        IFeatureBusinessMappingRepository FeatureBusinessMappings { get; }
        IFewShotExampleRepository FewShotExamples { get; }
        Task<int> SaveChangesAsync();
    }
}
