using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.AI;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate
{
    public class AccuracyAssessmentService : IAccuracyAssessmentService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<AccuracyAssessmentService> _logger;
        private readonly IDistributedCache _cache;
        private readonly ISemanticMappingHelper _semanticMappingHelper;
        private readonly IEvaluationLlmService _evaluationLlmService;

        public AccuracyAssessmentService(
            IEmbeddingService embeddingService,
            IEvaluationLlmService evaluationLlmService,
            ILogger<AccuracyAssessmentService> logger,
            IDistributedCache cache,
            ISemanticMappingHelper semanticMappingHelper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _evaluationLlmService = evaluationLlmService ?? throw new ArgumentNullException(nameof(evaluationLlmService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _semanticMappingHelper = semanticMappingHelper ?? throw new ArgumentNullException(nameof(semanticMappingHelper));
        }

        /// <inheritdoc/>
        public async Task<BatchAccuracyAssessmentResultDto> AssessAccuracyBatchAsync(Repo_Into_Graph_Application.Dtos.QuestionGenerate.GenerateQuestionsResponse response, WorkflowDataDto workflowData)
        {
            var batchResult = new BatchAccuracyAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName
            };

            if (workflowData == null || workflowData.Nodes == null || workflowData.Nodes.Count == 0)
                return batchResult;

            // Check cache first to avoid redundant processing
            var questionsList = response.GeneratedQuestionDtos ?? Enumerable.Empty<Repo_Into_Graph_Application.Dtos.QuestionGenerate.GeneratedQuestionDto>();
            string cacheKey = $"accuracy_batch_{response.BusinessId}_{string.Join("_", questionsList.Select(q => q.Question.GetHashCode()))}";

            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("[AssessAccuracyBatchAsync] Đã lấy kết quả đánh giá từ Redis/StashUp Cache.");
                return JsonSerializer.Deserialize<BatchAccuracyAssessmentResultDto>(cachedData)!;
            }

            // Precompute node embeddings ONCE for the entire batch
            var nodeTexts = workflowData.Nodes.Select(n => $"{n.NodeName}. {n.Description}".Trim()).ToList();
            var nodeVectors = await _embeddingService.EmbedBatchAsync(nodeTexts, "search_document");

            // Create a string representation of the workflow context for LLM evaluation
            var sb = new StringBuilder();
            sb.AppendLine($"Workflow Name: {workflowData.WorkflowName}");
            sb.AppendLine("Nodes:");
            if (workflowData.Nodes != null)
            {
                foreach (var n in workflowData.Nodes) sb.AppendLine($"- [{n.NodeId}] {n.NodeName}");
            }
            sb.AppendLine("Edges:");
            if (workflowData.Edges != null)
            {
                foreach (var e in workflowData.Edges) sb.AppendLine($"- {e.FromNodeId} -> {e.ToNodeId}");
            }
            string workflowContext = sb.ToString();

            foreach (var q in response.GeneratedQuestionDtos ?? Enumerable.Empty<Repo_Into_Graph_Application.Dtos.QuestionGenerate.GeneratedQuestionDto>())
            {
                if (string.IsNullOrWhiteSpace(q.Question)) continue;

                // Steps 1 & 2 & 3 manually for each question reusing nodeVectors
                var extractedPath = await _semanticMappingHelper.GetSemanticMappingAsync(response.BusinessId, q.Question, workflowData.Nodes, nodeVectors, q.TargetedEntryPoints);

                // Gọi LLM as a Judge để xác thực với Rubric
                var (isAccurate, brokenTransitions, finalVerdict, rubricScores, overallScore) = await ValidateAccuracyWithJudgeAsync(q.Question, workflowContext, extractedPath, workflowData.Nodes);

                batchResult.QuestionResults.Add(new QuestionAccuracyAssessmentResultDto
                {
                    Question = q.Question,
                    AccuracyResult = new AccuracyAssessmentResultDto
                    {
                        IsAccurate = isAccurate,
                        AccuracyScore = CalculateAccuracyScore(isAccurate, extractedPath.Count, brokenTransitions.Count),
                        ExtractedPath = extractedPath,
                        BrokenTransitions = brokenTransitions,
                        FinalVerdict = finalVerdict,
                        RubricScores = rubricScores,
                        OverallScore = overallScore
                    }
                });

                // Tách câu hỏi ra và gửi từng câu: Chờ 15 giây giữa các lần gọi để Groq hồi phục Token (12.000 TPM limit)
                _logger.LogInformation("[AssessAccuracyBatchAsync] Đã chấm xong 1 câu, đang nghỉ 15s để tránh Rate Limit...");
                await Task.Delay(TimeSpan.FromSeconds(15));
            }

            // Save the batch result to Redis/StashUp Cache for future requests
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(batchResult), cacheOptions);
            _logger.LogInformation("[AssessAccuracyBatchAsync] Đã lưu kết quả vào Redis/StashUp Cache.");

            return batchResult;
        }

        public async Task<AccuracyAssessmentResultDto> AssessAccuracyAsync(
            AccuracyAssessmentRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            if (string.IsNullOrWhiteSpace(request.Question))
                throw new ArgumentException("Câu hỏi (Question) không được để trống.", nameof(request));

            var nodes = request.WorkflowData?.Nodes ?? new List<WorkflowNodeInputDto>();
            var edges = request.WorkflowData?.Edges ?? new List<WorkflowEdgeInputDto>();

            _logger.LogInformation(
                "[AccuracyAssessment] Bắt đầu – WorkflowName='{W}' | {N} nút | {E} cạnh | Question='{Q}'",
                request.WorkflowData?.WorkflowName, nodes.Count, edges.Count,
                request.Question.Length > 80 ? request.Question[..80] + "…" : request.Question);

            // BƯỚC 1: Semantic-to-Node Mapping (Gemini Embedding thực)
            var extractedPath = await _semanticMappingHelper.GetSemanticMappingAsync(Guid.Empty, request.Question, nodes);

            _logger.LogInformation("[Bước 1] Ánh xạ thành công {Count} nút Active.", extractedPath.Count);

            // BƯỚC 2: LLM-as-a-Judge (Gemini Flash)
            var sb = new StringBuilder();
            sb.AppendLine($"Workflow Name: {request.WorkflowData?.WorkflowName}");
            sb.AppendLine("Nodes:");
            foreach (var n in nodes) sb.AppendLine($"- [{n.NodeId}] {n.NodeName}");
            sb.AppendLine("Edges:");
            foreach (var e in edges) sb.AppendLine($"- {e.FromNodeId} -> {e.ToNodeId}");
            string workflowContext = sb.ToString();

            var (isAccurate, brokenTransitions, finalVerdict, rubricScores, overallScore) = await ValidateAccuracyWithJudgeAsync(
                request.Question, workflowContext, extractedPath, nodes);

            _logger.LogInformation("[LLM Verification] IsAccurate={A} | Broken={B} | OverallScore={S}", isAccurate, brokenTransitions.Count, overallScore);

            return new AccuracyAssessmentResultDto
            {
                IsAccurate = isAccurate,
                AccuracyScore = CalculateAccuracyScore(isAccurate, extractedPath.Count, brokenTransitions.Count),
                ExtractedPath = extractedPath,
                BrokenTransitions = brokenTransitions,
                FinalVerdict = finalVerdict,
                RubricScores = rubricScores,
                OverallScore = overallScore
            };
        }

        private double CalculateAccuracyScore(bool isAccurate, int extractedPathCount, int brokenTransitionsCount)
        {
            if (isAccurate) return 1.0;
            if (extractedPathCount <= 1) return 0.0;

            int totalTransitions = extractedPathCount - 1;
            int validTransitions = Math.Max(0, totalTransitions - brokenTransitionsCount);
            return Math.Round((double)validTransitions / totalTransitions, 2);
        }

        // BƯỚC 2: Validate Accuracy With LLM Judge
        private async Task<(bool IsAccurate, List<BrokenTransitionDto> BrokenTransitions, string FinalVerdict, RubricScoresDto RubricScores, int OverallScore)> ValidateAccuracyWithJudgeAsync(
            string question,
            string workflowContext,
            List<ExtractedPathStepDto> extractedPath,
            List<WorkflowNodeInputDto> allNodes)
        {
            var systemInstruction = @"Bạn là chuyên gia kiểm định chất lượng LLM (LLM Evaluator) và quy trình nghiệp vụ (Business Analyst).
Nhiệm vụ của bạn là đánh giá một CÂU HỎI TÌNH HUỐNG (được sinh tự động) dựa trên đồ thị LUỒNG NGHIỆP VỤ (Workflow).
Bạn sẽ được cung cấp:
1. Câu hỏi cần đánh giá.
2. Cấu trúc đồ thị luồng nghiệp vụ (Nodes và Edges).
3. Chuỗi các bước (Nodes) được trích xuất từ câu hỏi (chỉ là gợi ý, có thể thiếu hoặc dư).

BẠN PHẢI CHẤM ĐIỂM CÂU HỎI THEO 5 TIÊU CHÍ (Thang điểm 1-5 cho mỗi tiêu chí, trong đó 5 là Tốt nhất, 1 là Tệ nhất):
1. Correctness (Tính đúng đắn logic): Trình tự các hành động trong câu hỏi có diễn ra đúng theo logic luồng (A phải xảy ra trước B) trên đồ thị không?
2. Faithfulness (Tính bám sát / Không ảo giác): Câu hỏi có KHÔNG bịa ra (hallucinate) các sự kiện, thuật ngữ, hoặc bước nghiệp vụ không hề tồn tại trong đồ thị không?
3. Context Relevance (Độ liên quan ngữ cảnh): Câu hỏi có thực sự trích xuất đúng thông tin trọng tâm và có ích từ luồng nghiệp vụ không?
4. Clarity (Tính mạch lạc & Rõ ràng): Câu hỏi có tự nhiên, dễ hiểu, không mơ hồ và đúng ngữ pháp không?
5. Answerability (Khả năng trả lời được): Với lượng kiến thức có trong đồ thị luồng cung cấp, người dùng có thực sự đủ dữ kiện để trả lời được câu hỏi đó không?

QUY TẮC ĐÁNH GIÁ ĐỨT GÃY LUỒNG (isAccurate & brokenTransitions):
- Ưu tiên cao nhất là logic thực sự của câu hỏi.
- Bỏ qua các bước bị thiếu (sub-step omission) hoặc dư thừa (noise nodes) nếu câu hỏi tổng thể vẫn hợp lý.
- Chỉ bắt lỗi (brokenTransitions) khi: Câu hỏi đi ngược trình tự đồ thị, kết hợp sai nhánh XOR, hoặc nhắc đến bước không thể xảy ra.
- Nếu không có bước nào trích xuất được nhưng câu hỏi vẫn phản ánh đúng nghiệp vụ, hãy cho isAccurate = true.

YÊU CẦU ĐẦU RA (Trả về ĐÚNG định dạng JSON sau, TUYỆT ĐỐI không có markdown text):
{
  ""scores"": {
    ""correctness"": 5,
    ""faithfulness"": 5,
    ""contextRelevance"": 4,
    ""clarity"": 5,
    ""answerability"": 4
  },
  ""isAccurate"": true,
  ""brokenTransitions"": [
    {
      ""fromNode"": ""Node_A"",
      ""toNode"": ""Node_B"",
      ""reason"": ""Lý do chi tiết""
    }
  ],
  ""finalVerdict"": ""Một đoạn văn tiếng Việt ngắn gọn (3-5 câu) phân tích tổng quan tại sao câu hỏi nhận được số điểm trên.""
}";

            var prompt = new StringBuilder();
            prompt.AppendLine("--- LUỒNG NGHIỆP VỤ (WORKFLOW) ---");
            prompt.AppendLine(workflowContext);
            prompt.AppendLine();
            prompt.AppendLine("--- CÂU HỎI CẦN ĐÁNH GIÁ ---");
            prompt.AppendLine(question);
            prompt.AppendLine();
            prompt.AppendLine("--- CÁC BƯỚC ĐÃ ĐƯỢC TRÍCH XUẤT TỪ CÂU HỎI ---");
            if (extractedPath.Count > 0)
            {
                foreach (var step in extractedPath)
                {
                    prompt.AppendLine($"Bước {step.Step}: [{step.NodeId}] {step.NodeName}");
                    var nodeInfo = allNodes.FirstOrDefault(n => n.NodeId == step.NodeId);
                    if (nodeInfo != null && !string.IsNullOrWhiteSpace(nodeInfo.SourceCode))
                    {
                        prompt.AppendLine("```csharp");
                        prompt.AppendLine(nodeInfo.SourceCode);
                        prompt.AppendLine("```");
                    }
                }
            }
            else
            {
                prompt.AppendLine("(Không tìm thấy bước nào trong luồng khớp với câu hỏi này)");
            }

            try
            {
                var aiJsonText = await _evaluationLlmService.EvaluateWithLlmAsync(systemInstruction, prompt.ToString());
                if (string.IsNullOrWhiteSpace(aiJsonText))
                {
                    return (false, new List<BrokenTransitionDto>(), "Lỗi hệ thống khi gọi LLM Evaluation API.", new RubricScoresDto(), 0);
                }

                // Clean markdown blocks if any
                if (aiJsonText.StartsWith("```json")) aiJsonText = aiJsonText.Substring(7);
                if (aiJsonText.EndsWith("```")) aiJsonText = aiJsonText.Substring(0, aiJsonText.Length - 3);
                aiJsonText = aiJsonText.Trim();

                var result = JsonSerializer.Deserialize<JsonElement>(aiJsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                bool isAccurate = result.TryGetProperty("isAccurate", out var isAccProp) && (isAccProp.ValueKind == JsonValueKind.True || isAccProp.ValueKind == JsonValueKind.False) ? isAccProp.GetBoolean() : false;
                string finalVerdict = result.TryGetProperty("finalVerdict", out var verdictProp) ? verdictProp.GetString() ?? "" : "";

                var broken = new List<BrokenTransitionDto>();
                if (result.TryGetProperty("brokenTransitions", out var brokenProp) && brokenProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in brokenProp.EnumerateArray())
                    {
                        broken.Add(new BrokenTransitionDto
                        {
                            FromNode = item.TryGetProperty("fromNode", out var f) ? f.GetString() : "",
                            ToNode = item.TryGetProperty("toNode", out var t) ? t.GetString() : "",
                            Reason = item.TryGetProperty("reason", out var r) ? r.GetString() : ""
                        });
                    }
                }

                var rubric = new RubricScoresDto();
                int overall = 0;
                if (result.TryGetProperty("scores", out var scoresProp) && scoresProp.ValueKind == JsonValueKind.Object)
                {
                    rubric.Correctness = scoresProp.TryGetProperty("correctness", out var c) ? c.GetInt32() : 0;
                    rubric.Faithfulness = scoresProp.TryGetProperty("faithfulness", out var fScore) ? fScore.GetInt32() : 0;
                    rubric.ContextRelevance = scoresProp.TryGetProperty("contextRelevance", out var cr) ? cr.GetInt32() : 0;
                    rubric.Clarity = scoresProp.TryGetProperty("clarity", out var cl) ? cl.GetInt32() : 0;
                    rubric.Answerability = scoresProp.TryGetProperty("answerability", out var ans) ? ans.GetInt32() : 0;

                    overall = rubric.Correctness + rubric.Faithfulness + rubric.ContextRelevance + rubric.Clarity + rubric.Answerability;
                }

                return (isAccurate, broken, finalVerdict, rubric, overall);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM Assessment parsing failed.");
                return (false, new List<BrokenTransitionDto>(), "Lỗi hệ thống.", new RubricScoresDto(), 0);
            }
        }
    }
}