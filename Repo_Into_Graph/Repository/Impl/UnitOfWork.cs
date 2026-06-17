using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Repository.Impl
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AnalysisDbContext _context;
        private bool _disposed;

        private IAnalysisRunRepository? _analysisRuns;
        private ICallGraphEdgeRepository? _callGraphEdges;
        private IMethodSourceRepository? _methodSources;
        private IFeatureRepository? _features;
        private IFeatureMethodMappingRepository? _featureMethodMappings;
        private IFewShotExampleRepository? _fewShotExamples;
        private IBusinessFlowRepository? _businessFlows;

        public UnitOfWork(AnalysisDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IAnalysisRunRepository AnalysisRuns =>
            _analysisRuns ??= new AnalysisRunRepository(_context);

        public ICallGraphEdgeRepository CallGraphEdges =>
            _callGraphEdges ??= new CallGraphEdgeRepository(_context);

        public IMethodSourceRepository MethodSources =>
            _methodSources ??= new MethodSourceRepository(_context);

        public IFeatureRepository Features =>
            _features ??= new FeatureRepository(_context);

        public IFeatureMethodMappingRepository FeatureMethodMappings =>
            _featureMethodMappings ??= new FeatureMethodMappingRepository(_context);

        public IFewShotExampleRepository FewShotExamples =>
            _fewShotExamples ??= new FewShotExampleRepository(_context);

        public IBusinessFlowRepository BusinessFlows =>
            _businessFlows ??= new BusinessFlowRepository(_context);

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                _disposed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    await _context.DisposeAsync();
                }
                _disposed = true;
            }
        }
    }
}
