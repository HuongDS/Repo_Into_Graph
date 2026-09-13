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
using Repo_Into_Graph_Application.Helper;
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
                // Xác định ngôn ngữ THẬT của method. Ưu tiên cột Language đã lưu lúc phân
                // tích repo; nếu null (bản ghi tạo trước migration AddLanguageToMethodSource)
                // thì LanguageNormalizer suy luận từ chính mã nguồn.
                //
                // Trước đây chỗ này hardcode "csharp" và KHÔNG truyền Language xuống
                // RouterRequestDto, nên mọi method Java đều bị Tầng 1 phân tích bằng
                // tree-sitter C# => V(G) và SLOC sai => định tuyến sai, và Tầng 2 dựng CFG
                // trên cây cú pháp sai. Lỗi này im lặng, không ném exception.
                bool languageSupported = LanguageNormalizer.TryNormalize(
                    method.Language, method.SourceCode, out string language);

                var functionNode = new FunctionNode
                {
                    NodeName = $"{method.ClassName}.{method.MethodName}",
                    Language = language,
                    RouteType = "ROUTE_RAW_CODE",
                    SourceCode = method.SourceCode
                };

                // Tầng 1 + Tầng 2 chỉ hỗ trợ Java và C#. Với ngôn ngữ khác (Python, Node.js)
                // Python Microservice trả HTTP 400; gửi lên chỉ tốn một vòng mạng rồi rơi vào
                // nhánh lỗi. Đi thẳng bằng mã nguồn gốc là hành vi đúng và an toàn.
                if (!languageSupported)
                {
                    functionNodes.Add(functionNode);
                    continue;
                }

                // Tầng 1: Đánh giá Router
                var routerRequest = new RouterRequestDto
                {
                    ModuleId = method.MethodName,
                    SourceCode = method.SourceCode,
                    Language = language
                };
                var routerDecision = await _routerService.EvaluateCodeContextAsync(routerRequest);

                string routeType = routerDecision?.SelectedRoute == Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter.RoutingType.HybridGraph ? "ROUTE_HYBRID" : "ROUTE_RAW_CODE";

                // Tầng 2: Nếu là ROUTE_HYBRID thì dùng lại kết quả Tầng 2 mà Tầng 1 đã
                // sinh sẵn (routerDecision.HybridContextResult) — KHÔNG gọi lại
                // _hybridContextService.GenerateAsync lần nữa (tránh gọi trùng Python
                // Microservice và tốn thời gian vô ích).
                if (routeType == "ROUTE_HYBRID")
                {
                    var hybridResult = routerDecision?.HybridContextResult;

                    // Chỉ dùng ngữ cảnh lai khi Tầng 2 chạy thành công VÀ thực sự tiết
                    // kiệm token so với mã nguồn gốc. hybrid_prompt là bản đã được Tầng 2
                    // TỐI ƯU SẴN (CFG + critical logic đã lọc theo trọng số + metadata) —
                    // phải dùng thẳng field này thay vì tự ghép CfgSkeleton với TOÀN BỘ
                    // CriticalSnippets (chưa lọc, tối đa 40 đoạn) vì cách ghép thủ công đó
                    // vừa trùng lặp thông tin với CFG vừa không lọc theo trọng số, nên
                    // thường DÀI HƠN cả mã nguồn gốc — đây chính là lý do token vượt quá
                    // cách gửi code base thuần.
                    bool hybridIsUsable = hybridResult != null
                        && hybridResult.Status != "FAILED"
                        && !string.IsNullOrWhiteSpace(hybridResult.HybridPrompt)
                        && hybridResult.Metrics.EstimatedTokenSavingPct > 0;

                    if (hybridIsUsable)
                    {
                        functionNode.RouteType = "ROUTE_HYBRID";
                        functionNode.HybridPrompt = hybridResult!.HybridPrompt;
                    }
                    // Nếu Tầng 2 lỗi hoặc ngữ cảnh lai không nhỏ hơn mã nguồn gốc,
                    // functionNode giữ nguyên ROUTE_RAW_CODE + SourceCode đã gán ở trên
                    // (an toàn, không bao giờ tốn token hơn cách gửi code base thuần).
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

            // ════════════════════════════════════════════════════════════════
            // 6. TẦNG 3 — DÙNG CHUNG PROMPT VỚI TRADITIONAL VÀ GRAPH
            //
            // Trước đây E2E đi qua PromptBuilderService — một prompt HOÀN TOÀN KHÁC:
            //   - Xin "targetNode" (MỘT node, số ít) thay vì "targetedEntryPoints"
            //     (mảng 3-4 mắt xích Controller -> Service -> ServiceImpl -> Repository)
            //   - Xin câu TRẮC NGHIỆM A/B/C/D thay vì câu hỏi tình huống mở
            //   - Không nạp few-shot examples trong khi Traditional/Graph có nạp
            //
            // Hệ quả đo được: đường đi trích xuất của E2E luôn chỉ có 1 node
            // => L_q = 0 và V(G) = 1 với PHƯƠNG SAI ĐÚNG BẰNG 0 trên mọi nghiệp vụ
            // => accuracyScore = (bước hợp lệ / tổng bước) không có bước nào để sai
            // => E2E "thắng" ~95% một cách TẤT YẾU VỀ MẶT TOÁN HỌC, không phải do
            //    chất lượng ngữ cảnh.
            //
            // Thí nghiệm khi đó lẫn ba biến: biểu diễn ngữ cảnh (biến cần đo), định dạng
            // câu hỏi, và số node được khai báo. Không cô lập được cái nào.
            //
            // Nay cả ba phương pháp gọi CHUNG GenerateUnifiedQuestionsAsync. Khác biệt
            // duy nhất còn lại là NỘI DUNG hai khe ngữ cảnh:
            //     Traditional : codeBuilder = mã nguồn thô     | contextBuilder = (rỗng)
            //     Graph       : codeBuilder = (rỗng)           | contextBuilder = Mermaid
            //     E2E         : codeBuilder = ngữ cảnh ĐÃ ĐỊNH TUYẾN (raw hoặc CFG nén
            //                   tuỳ độ phức tạp từng hàm)      | contextBuilder = tổng quan luồng
            // ════════════════════════════════════════════════════════════════
            string aggregatedContext = _contextAggregatorService.AggregateContext(functionNodes);

            int numQs = request.NumberOfQuestions > 0 && request.NumberOfQuestions <= 20 ? request.NumberOfQuestions : 5;

            // Nạp few-shot GIỐNG HỆT QuestionGenerate, nếu không đây lại là một biến lẫn nữa.
            IEnumerable<Repo_Into_Graph_DataAccess.Models.FewShot.FewShotExample>? fewShotExamples = null;
            if (request.FewShotExampleIds != null && request.FewShotExampleIds.Count > 0)
            {
                fewShotExamples = await _unitOfWork.FewShotExamples.GetByIdsAsync(request.FewShotExampleIds);
            }
            else if (!string.IsNullOrWhiteSpace(request.Difficulty))
            {
                fewShotExamples = await _unitOfWork.FewShotExamples.GetByDifficultyAsync(request.Difficulty, 5);
            }

            var (questions, inputTokens, outputTokens) = await _aiService.GenerateUnifiedQuestionsAsync(
                businessName: businessModel.BusinessName,
                codeBuilder: aggregatedContext,
                contextBuilder: workflowOverview,
                numberOfQuestions: numQs,
                difficulty: request.Difficulty,
                additionalContext: request.Description,
                fewShotExamples: fewShotExamples);

            // 7. Trả về Response
            return new GenerateQuestionsResponse
            {
                BusinessId = businessModel.Id,
                BusinessName = businessModel.BusinessName,
                EntryPoint = string.Join(", ", features.Select(f => f.EntryPoint)),
                TotalSteps = features.Sum(f => f.Steps?.Count ?? 0),
                FewShotUsed = fewShotExamples?.Count() ?? 0,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                GeneratedQuestionDtos = questions
            };
        }
    }
}
