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
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Caching.Distributed;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.AI;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate
{
    public class AccuracyAssessmentService : IAccuracyAssessmentService
    {
        private const double SimilarityThreshold = 0.55;
        private const string EmbeddingModel = "embed-multilingual-v3.0";
        private const string VerdictModel = "command-r-08-2024";

        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<AccuracyAssessmentService> _logger;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _geminiApiKey;
        private readonly IDistributedCache _cache;
        private readonly ISemanticMappingHelper _semanticMappingHelper;

        public AccuracyAssessmentService(
            IEmbeddingService embeddingService, 
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration, 
            ILogger<AccuracyAssessmentService> logger,
            IDistributedCache cache,
            ISemanticMappingHelper semanticMappingHelper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _geminiApiKey = configuration["GeminiConfig:ApiKey"] ?? throw new InvalidOperationException("Thiếu cấu hình GeminiConfig:ApiKey.");
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

            // 1. Kiểm tra Cache trước
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

            // Tạo Workflow Context text để gửi cho Gemini
            var sb = new StringBuilder();
            sb.AppendLine($"Workflow Name: {workflowData.WorkflowName}");
            sb.AppendLine("Nodes:");
            if (workflowData.Nodes != null)
            {
                foreach(var n in workflowData.Nodes) sb.AppendLine($"- [{n.NodeId}] {n.NodeName}");
            }
            sb.AppendLine("Edges:");
            if (workflowData.Edges != null)
            {
                foreach(var e in workflowData.Edges) sb.AppendLine($"- {e.FromNodeId} -> {e.ToNodeId}");
            }
            string workflowContext = sb.ToString();

            foreach (var q in response.GeneratedQuestionDtos ?? Enumerable.Empty<Repo_Into_Graph_Application.Dtos.QuestionGenerate.GeneratedQuestionDto>())
            {
                if (string.IsNullOrWhiteSpace(q.Question)) continue;

                // Steps 1 & 2 & 3 manually for each question reusing nodeVectors
                var extractedPath = await _semanticMappingHelper.GetSemanticMappingAsync(response.BusinessId, q.Question, workflowData.Nodes, nodeVectors);
                
                // Gọi LLM as a Judge để xác thực
                var (isAccurate, brokenTransitions, finalVerdict) = await ValidateAccuracyWithGeminiAsync(q.Question, workflowContext, extractedPath, workflowData.Nodes);

                batchResult.QuestionResults.Add(new QuestionAccuracyAssessmentResultDto
                {
                    Question = q.Question,
                    AccuracyResult = new AccuracyAssessmentResultDto
                    {
                        IsAccurate = isAccurate,
                        AccuracyScore = CalculateAccuracyScore(isAccurate, extractedPath.Count, brokenTransitions.Count),
                        ExtractedPath = extractedPath,
                        BrokenTransitions = brokenTransitions,
                        FinalVerdict = finalVerdict
                    }
                });
            }

            // 2. Lưu vào Cache (Tồn tại trong 60 phút)
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
            foreach(var n in nodes) sb.AppendLine($"- [{n.NodeId}] {n.NodeName}");
            sb.AppendLine("Edges:");
            foreach(var e in edges) sb.AppendLine($"- {e.FromNodeId} -> {e.ToNodeId}");
            string workflowContext = sb.ToString();

            var (isAccurate, brokenTransitions, finalVerdict) = await ValidateAccuracyWithGeminiAsync(
                request.Question, workflowContext, extractedPath, nodes);

            _logger.LogInformation("[LLM Verification] IsAccurate={A} | Broken={B}", isAccurate, brokenTransitions.Count);

            return new AccuracyAssessmentResultDto
            {
                IsAccurate        = isAccurate,
                AccuracyScore     = CalculateAccuracyScore(isAccurate, extractedPath.Count, brokenTransitions.Count),
                ExtractedPath     = extractedPath,
                BrokenTransitions = brokenTransitions,
                FinalVerdict      = finalVerdict
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


        // BƯỚC 2: Validate Accuracy With Gemini (LLM-as-a-Judge)
        private async Task<(bool IsAccurate, List<BrokenTransitionDto> BrokenTransitions, string FinalVerdict)> ValidateAccuracyWithGeminiAsync(
            string question,
            string workflowContext,
            List<ExtractedPathStepDto> extractedPath,
            List<WorkflowNodeInputDto> allNodes)
        {
            var systemInstruction = @"Bạn là chuyên gia kiểm định quy trình nghiệp vụ (Business Analyst).
Nhiệm vụ của bạn là đánh giá xem một CÂU HỎI TÌNH HUỐNG có phản ánh ĐÚNG logic của LUỒNG NGHIỆP VỤ (Workflow) hay không.
Bạn sẽ được cung cấp:
1. Câu hỏi.
2. Cấu trúc đồ thị luồng nghiệp vụ (Nodes và Edges).
3. Chuỗi các bước (Nodes) được trích xuất từ câu hỏi (Đây chỉ là gợi ý từ hệ thống tìm kiếm ngữ nghĩa, có thể thiếu sót hoặc dư thừa).

QUY TẮC ĐÁNH GIÁ TÍNH CHÍNH XÁC (IsAccurate):
1. Ưu tiên cao nhất là đánh giá ý nghĩa logic của CÂU HỎI đối chiếu với ĐỒ THỊ LUỒNG.
2. BỎ QUA LỖI THIẾU BƯỚC (Sub-step omission): Nếu câu hỏi chỉ nhắc đến một bước tổng quát (Ví dụ: Service layer kiểm tra giới hạn) mà bước đó ngầm gọi đến các bước con (Ví dụ: Repository layer đếm số lượng), thì câu hỏi hoàn toàn HỢP LỆ và CHÍNH XÁC.
3. BỎ QUA LỖI NODE DƯ THỪA (Noise nodes): Danh sách trích xuất có thể chứa các Endpoint/Node độc lập hoặc dư thừa do thuật toán tìm kiếm từ khóa bắt nhầm (Ví dụ: Câu hỏi nói về hàm A có cập nhật trạng thái, thuật toán lại trích xuất thêm hàm B cũng tên là cập nhật trạng thái). BẠN PHẢI BỎ QUA các node không liên quan này thay vì đánh giá chúng là lỗi đứt gãy luồng (Broken Transition). Chỉ tập trung vào các Node thực sự thuộc về luồng nghiệp vụ đang xét.
4. CHỈ BẮT LỖI SAI LOGIC KHI:
   - Câu hỏi mô tả một trình tự hoàn toàn ngược ngạo (A xảy ra sau B, nhưng trong đồ thị B xảy ra sau A).
   - Câu hỏi cố tình kết hợp 2 nhánh rẽ đối lập (XOR) bắt buộc phải chạy đồng thời.
   - Câu hỏi nhắc đến một sự kiện ảo không hề tồn tại hoặc không thể đi tới được trong luồng chính.
5. NHÁNH SONG SONG vs ĐỨT GÃY TUẦN TỰ: Nếu 2 nodes được trích xuất là 2 nhánh kiểm tra song song hoặc độc lập từ cùng một quy trình (ví dụ: cùng kiểm tra các điều kiện khác nhau), việc chúng không nối trực tiếp với nhau là HỢP LỆ (Bỏ qua lỗi đứt gãy). TUY NHIÊN, nếu câu hỏi mô tả 2 hành động bắt buộc phải xảy ra tuần tự (A phải xong rồi mới tới B) mà chúng không hề có đường đi liên kết trực tiếp hay gián tiếp trên đồ thị, thì đó LÀ LỖI ĐỨT GÃY THỰC SỰ.
6. NẾU KHÔNG CÓ BƯỚC NÀO ĐƯỢC TRÍCH XUẤT (Extracted Path rỗng): Bạn hãy tự dựa vào Đồ thị luồng để đánh giá xem câu hỏi có hợp lý về mặt nghiệp vụ không. Nếu hợp lý (nói đúng tên, đúng luồng, đúng điều kiện), hãy trả về IsAccurate = true và brokenTransitions rỗng. Đừng tự tạo ra lỗi đứt gãy nếu không thực sự chắc chắn.

YÊU CẦU ĐẦU RA (Trả về ĐÚNG định dạng JSON sau, TUYỆT ĐỐI không có markdown ```json):
{
  ""isAccurate"": true,
  ""brokenTransitions"": [
    {
      ""fromNode"": ""Tên_Node_1"",
      ""toNode"": ""Tên_Node_2"",
      ""reason"": ""Giải thích lý do tại sao bước nhảy này sai logic luồng.""
    }
  ],
  ""finalVerdict"": ""Một đoạn văn tiếng Việt ngắn gọn (3-5 câu) phân tích kết luận cuối cùng.""
}
Lưu ý: Nếu isAccurate = true, hãy để mảng brokenTransitions rỗng [].";

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

            var clientOptions = new ClientOptions { HttpClientFactory = () => _httpClientFactory.CreateClient() };
            var client = new Client(apiKey: _geminiApiKey, clientOptions: clientOptions);

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content { Parts = [new Part { Text = systemInstruction }] },
                Temperature = 0.2f,
                ResponseMimeType = "application/json"
            };

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await client.Models.GenerateContentAsync(
                        model: "gemini-3.1-flash-lite",
                        contents: prompt.ToString(),
                        config: config
                    );

                    var aiJsonText = response.Text?.Trim() ?? string.Empty;

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

                    return (isAccurate, broken, finalVerdict);
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Gemini Accuracy Assessment failed after {Retries} attempts.", maxRetries);
                        return (false, new List<BrokenTransitionDto>(), "Lỗi hệ thống khi gọi Gemini API để phân tích câu hỏi.");
                    }
                    await Task.Delay(2000 * attempt);
                }
            }

            return (false, new List<BrokenTransitionDto>(), "Lỗi hệ thống.");
        }

        // HELPERS: Sliding Window Chunks

        /// <summary>
        /// Tách văn bản thành các chunk cửa sổ trượt (sliding window) để xử lý ngữ nghĩa.
        /// </summary>
        private static List<(string Chunk, int StartPos)> BuildSlidingWindowChunks(
            string text,
            int windowSize = 5,
            int stepSize   = 2)
        {
            var result = new List<(string, int)>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Tách câu thành danh sách (word, startIndex)
            var words = new List<(string Word, int StartIdx)>();
            int i     = 0;
            while (i < text.Length)
            {
                // Bỏ qua khoảng trắng
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (i >= text.Length) break;

                int wordStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                words.Add((text[wordStart..i], wordStart));
            }

            // Tạo sliding window chunks
            for (int w = 0; w <= words.Count - windowSize; w += stepSize)
            {
                var chunk     = string.Join(" ", words.Skip(w).Take(windowSize).Select(x => x.Word));
                int startPos  = words[w].StartIdx;
                result.Add((chunk, startPos));
            }

            // Đảm bảo luôn có ít nhất 1 chunk (toàn bộ câu hỏi)
            if (result.Count == 0)
                result.Add((text.Trim(), 0));

            return result;
        }
    }
}
