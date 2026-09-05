using Microsoft.Extensions.DependencyInjection;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Services.Analysis;
using Repo_Into_Graph_Application.Services.Caculation;
using Repo_Into_Graph_Application.Services.Features;
using Repo_Into_Graph_Application.Services.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using Repo_Into_Graph_Application.Services.DataFlowParser;
using Repo_Into_Graph_Application.Services.Features;
using Repo_Into_Graph_Application.Services.FewShot;
using Repo_Into_Graph_Application.Services.GitService;
using Repo_Into_Graph_Application.Services.HybridContextGenerator;
using Repo_Into_Graph_Application.Services.Mapper;
using Repo_Into_Graph_Application.Services.QuestionGenerate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.CoverageEvaluate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.DifficultyEvaluate;
using Repo_Into_Graph_DataAccess.Repository.Impl;
using Repo_Into_Graph_DataAccess.Repository.Interface;

namespace Repo_Into_Graph_API.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddProjectDependencies(this IServiceCollection services)
        {
            // Repositories
            services.AddScoped<IAnalysisRunRepository, AnalysisRunRepository>();
            services.AddScoped<ICallGraphEdgeRepository, CallGraphEdgeRepository>();
            services.AddScoped<IMethodSourceRepository, MethodSourceRepository>();
            services.AddScoped<IBusinessRepository, BusinessRepository>();
            services.AddScoped<IFeatureMethodMappingRepository, FeatureMethodMappingRepository>();
            services.AddScoped<IFeatureRepository, FeatureRepository>();
            services.AddScoped<IFeatureBusinessMappingRepository, FeatureBusinessMappingRepository>();
            services.AddScoped<IFewShotExampleRepository, FewShotExampleRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Application Services
            services.AddScoped<ICodeQueryable, CodeQueryable>();
            services.AddScoped<GraphMapperService>();
            services.AddScoped<IGitService, GitService>();
            services.AddScoped<IAnalysisService, AnalysisService>();
            services.AddScoped<IAnalysisRunService, AnalysisRunService>();
            services.AddScoped<IFeatureService, FeatureService>();
            services.AddScoped<IFewShotService, FewShotService>();
            services.AddScoped<BusinessFlowParser>();
            services.AddScoped<IQuestionGenerate, QuestionGenerate>();
            services.AddScoped<IAIService, AIService>();
            services.AddScoped<DataFlowParseService>();
            services.AddScoped<BusinessCallDataFlowGenerator>();
            services.AddScoped<ICaculationService, CaculationService>();

            // Workflow Assessment Services
            services.AddScoped<IWorkflowAssessmentService, WorkflowAssessmentService>();
            services.AddScoped<ICoverageAssessmentService, CoverageAssessmentService>();
            services.AddScoped<IAccuracyAssessmentService, AccuracyAssessmentService>();
            services.AddScoped<IEvaluationLlmService, DeepSeekEvaluationService>();
            services.AddScoped<IDifficultyAssessmentService, DifficultyAssessmentService>();
            services.AddScoped<ISemanticMappingHelper, SemanticMappingHelper>();

            // Adaptive Context Router (Tang 1)
            services.AddHttpClient<IAdaptiveContextRouterService, AdaptiveContextRouterService>();
            services.AddScoped<IAdaptiveContextRouterService, AdaptiveContextRouterService>();

            // Hybrid Context Generator (Tang 2 - Stub)
            services.AddScoped<IHybridContextGeneratorService, HybridContextGeneratorService>();

            // AI & Embedding
            services.AddScoped<Repo_Into_Graph_Application.Services.AI.IEmbeddingService, Repo_Into_Graph_Application.Services.AI.EmbeddingService>();

            // AutoMapper
            services.AddAutoMapper(cfg => cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies()));

            return services;
        }
    }
}
