using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_DataAccess.Database;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Repository.Impl
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AnalysisDbContext _context;
        private bool _disposed;
        private IAnalysisRunRepository? analysisRuns;
        private ICallGraphEdgeRepository? callGraphEdges;
        private IMethodSourceRepository? methodSources;
        private IBusinessRepository? businesses;
        private IFeatureMethodMappingRepository? featureMethodMappings;
        private IFeatureRepository? features;
        private IFeatureBusinessMappingRepository? featureBusinessMappings;
        private IFewShotExampleRepository? fewShotExamples;

        public UnitOfWork(AnalysisDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            analysisRuns = new AnalysisRunRepository(_context);
            callGraphEdges = new CallGraphEdgeRepository(_context);
            methodSources = new MethodSourceRepository(_context);
            businesses = new BusinessRepository(_context);
            featureMethodMappings = new FeatureMethodMappingRepository(_context);
            features = new FeatureRepository(_context);
            featureBusinessMappings = new FeatureBusinessMappingRepository(_context);
            fewShotExamples = new FewShotExampleRepository(_context);
        }
        public IAnalysisRunRepository AnalysisRuns => analysisRuns ??= new AnalysisRunRepository(_context);

        public ICallGraphEdgeRepository CallGraphEdges => callGraphEdges ??= new CallGraphEdgeRepository(_context);

        public IMethodSourceRepository MethodSources => methodSources ??= new MethodSourceRepository(_context);

        public IBusinessRepository Businesses => businesses ??= new BusinessRepository(_context);

        public IFeatureMethodMappingRepository FeatureMethodMappings => featureMethodMappings ??= new FeatureMethodMappingRepository(_context);
        public IFeatureRepository Features => features ??= new FeatureRepository(_context);
        public IFeatureBusinessMappingRepository FeatureBusinessMappings => featureBusinessMappings ??= new FeatureBusinessMappingRepository(_context);
        public IFewShotExampleRepository FewShotExamples => fewShotExamples ??= new FewShotExampleRepository(_context);
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
