using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;
using Repo_Into_Graph_Application.Dtos.LLMOrchestrator;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using Repo_Into_Graph_Application.Services.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Services.HybridContextGenerator;
using Repo_Into_Graph_DataAccess.Repository.Interface;

namespace Repo_Into_Graph_Application.Services.LLMOrchestrator
{
    public class E2EOrchestratorService : IE2EOrchestratorService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAdaptiveContextRouterService _routerService;
        private readonly IHybridContextGeneratorService _hybridContextService;
        private readonly IContextAggregatorService _contextAggregatorService;
        private readonly IPromptBuilderService _promptBuilderService;
        private readonly IAIService _aiService;

        public E2EOrchestratorService(
            IUnitOfWork unitOfWork,
            IAdaptiveContextRouterService routerService,
            IHybridContextGeneratorService hybridContextService,
            IContextAggregatorService contextAggregatorService,
            IPromptBuilderService promptBuilderService,
            IAIService aiService)
        {
            _unitOfWork = unitOfWork;
            _routerService = routerService;
            _hybridContextService = hybridContextService;
            _contextAggregatorService = contextAggregatorService;
            _promptBuilderService = promptBuilderService;
            _aiService = aiService;
        }

        public async Task<GenerateQuestionsResponse> GenerateE2EQuestionsAsync(GenerateQuestionsRequest request)
        {
            if (request == null)
                throw new BadRequestException("Yêu cầu không được để trống.");

            // 1. Load Business
            var businessModel = await _unitOfWork.Businesses.GetByIdAsync(request.BusinessId);
            if (businessModel == null)
                throw new NotFoundException("Business", request.BusinessId);

            // 2. Load các Feature (Luồng nghiệp vụ) được map với Business này
            var featureBusinessMappings = await _unitOfWork.FeatureBusinessMappings.GetFeatureIdsByBusinessIdAsync(request.BusinessId);
            var features = await _unitOfWork.Features.GetFeaturesWithStepsByIdsAsync(featureBusinessMappings);

            // 3. Load Source Code (MethodSource) từ các Feature đó
            var featureMethodMappings = await _unitOfWork.FeatureMethodMappings.GetMappingsWithMethodSourceByFeatureIdsAsync(featureBusinessMappings);

            var methodSources = featureMethodMappings
                .Where(m => m.MethodSource != null)
                .Select(m => m.MethodSource!)
                .DistinctBy(m => m.Id)
                .ToList();

            if (!methodSources.Any())
                throw new BadRequestException("Không tìm thấy Source Code nào được map cho Business này để sinh câu hỏi.");

            // Chuẩn bị danh sách FunctionNode cho Tầng 3
            var functionNodes = new List<FunctionNode>();

            // 4. Chạy Tầng 1 và Tầng 2 cho từng method
            foreach (var method in methodSources)
            {
                // Tầng 1: Đánh giá Router
                var routerRequest = new RouterRequestDto
                {
                    ModuleId = method.MethodName,
                    SourceCode = method.SourceCode
                };
                var routerDecision = await _routerService.EvaluateCodeContextAsync(routerRequest);
                
                string routeType = routerDecision?.SelectedRoute == Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter.RoutingType.HybridGraph ? "ROUTE_HYBRID" : "ROUTE_RAW_CODE";

                // Map language string for markdown fencing
                string language = "csharp";
                // Assuming extension or something could determine this, or default to csharp since backend mostly is C#.
                // For this E2E, we default to csharp for method sources.

                var functionNode = new FunctionNode
                {
                    NodeName = $"{method.ClassName}.{method.MethodName}",
                    Language = language,
                    RouteType = routeType,
                    SourceCode = method.SourceCode
                };

                // Tầng 2: Nếu là ROUTE_HYBRID thì sinh Hybrid CFG
                if (routeType == "ROUTE_HYBRID")
                {
                    var hybridInput = new HybridContextInputDto
                    {
                        ModuleId = method.MethodName,
                        Language = language,
                        RoutingDecision = routeType,
                        RawSourceCode = method.SourceCode
                    };
                    var hybridResult = await _hybridContextService.GenerateAsync(hybridInput);

                    if (hybridResult != null)
                    {
                        functionNode.CfgSkeleton = hybridResult.CfgSkeleton ?? string.Empty;
                        functionNode.CriticalSnippets = hybridResult.CriticalSnippets != null ? string.Join("\n", hybridResult.CriticalSnippets) : string.Empty;
                    }
                }

                functionNodes.Add(functionNode);
            }

            // 5. Gom Workflow Overview từ Features
            var overviewBuilder = new StringBuilder();
            overviewBuilder.AppendLine($"Business: {businessModel.BusinessName}");
            foreach (var feature in features)
            {
                overviewBuilder.AppendLine($"- Feature: {feature.Name}");
                if (!string.IsNullOrWhiteSpace(feature.EntryPoint))
                {
                    overviewBuilder.AppendLine($"  EntryPoint: {feature.EntryPoint}");
                }
            }
            string workflowOverview = overviewBuilder.ToString();

            // 6. Tầng 3: Build Prompt và Gọi AI
            string aggregatedContext = _contextAggregatorService.AggregateContext(functionNodes);
            
            int numQs = request.NumberOfQuestions > 0 && request.NumberOfQuestions <= 20 ? request.NumberOfQuestions : 5;
            string systemPrompt = _promptBuilderService.BuildSystemPrompt(numQs, request.Difficulty);
            string finalPayload = _promptBuilderService.BuildFinalPayload(workflowOverview, aggregatedContext);

            // Bổ sung Description từ người dùng vào payload nếu có
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                finalPayload += $"\n\n=== NGỮ CẢNH BỔ SUNG ===\n{request.Description}\n";
            }

            var aiResult = await _aiService.GenerateCodeQuestionsAsync(systemPrompt, finalPayload);
            var aiResponse = aiResult.Response;

            // 7. Trả về Response
            return new GenerateQuestionsResponse
            {
                BusinessId = businessModel.Id,
                BusinessName = businessModel.BusinessName,
                EntryPoint = string.Join(", ", features.Select(f => f.EntryPoint)),
                TotalSteps = features.Sum(f => f.Steps?.Count ?? 0),
                FewShotUsed = 0, // Không dùng Few-Shot theo cấu hình
                InputTokens = aiResult.InputTokens,
                OutputTokens = aiResult.OutputTokens, 
                GeneratedQuestionDtos = (aiResponse.Questions ?? new List<CodeQuestion>()).Select(q => new GeneratedQuestionDto 
                {
                    Question = q.QuestionText,
                    SuggestedAnswer = q.CorrectAnswer,
                    Difficulty = q.DifficultyLevel,
                    TargetedEntryPoints = new[] { q.TargetNode }
                }).ToList()
            };
        }
    }
}
