using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Interface
{
    public interface IUnitOfWork : IAsyncDisposable, IDisposable
    {
        IAnalysisRunRepository AnalysisRuns { get; }
        ICallGraphEdgeRepository CallGraphEdges { get; }
        IMethodSourceRepository MethodSources { get; }

        // Business (nhóm chức năng từ template)
        IBusinessRepository Businesses { get; }
        IBusinessMethodMappingRepository BusinessMethodMappings { get; }

        // Feature (luồng phân tích từ call graph)
        IFeatureRepository Features { get; }

        // Nhiều-nhiều Feature ↔ Business
        IFeatureBusinessMappingRepository FeatureBusinessMappings { get; }

        IFewShotExampleRepository FewShotExamples { get; }

        Task<int> SaveChangesAsync();
    }
}
