using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    public class AccuracyAssessmentService : IAccuracyAssessmentService
    {
        private const double SimilarityThreshold = 0.55;
        private const string EmbeddingModel = "embed-multilingual-v3.0";
        private const string VerdictModel = "command-r-08-2024";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _cohereApiKey;
        private readonly ILogger<AccuracyAssessmentService> _logger;

        public AccuracyAssessmentService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<AccuracyAssessmentService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _cohereApiKey = configuration["CohereConfig:ApiKey"]
                ?? throw new InvalidOperationException("Thiếu cấu hình CohereConfig:ApiKey.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC ENTRY POINT
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
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

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 1: Semantic-to-Node Mapping (Gemini Embedding thực)
            // ══════════════════════════════════════════════════════════════════
            var extractedPath = await Step1_EmbeddingMappingAsync(request.Question, nodes);

            _logger.LogInformation("[Bước 1] Ánh xạ thành công {Count} nút Active.", extractedPath.Count);

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 2: Graph Connection Verification (Path Alignment)
            // ══════════════════════════════════════════════════════════════════
            var (isAccurate, brokenTransitions) = Step2_GraphConnectionVerification(
                extractedPath, nodes, edges);

            _logger.LogInformation("[Bước 2] IsAccurate={A} | Broken={B}", isAccurate, brokenTransitions.Count);

            // ══════════════════════════════════════════════════════════════════
            // FINAL VERDICT: Sinh lời tổng hợp bằng Gemini text
            // ══════════════════════════════════════════════════════════════════
            var finalVerdict = await GenerateFinalVerdictAsync(
                request.Question,
                request.WorkflowData?.WorkflowName ?? string.Empty,
                extractedPath,
                brokenTransitions,
                isAccurate);

            return new AccuracyAssessmentResultDto
            {
                IsAccurate        = isAccurate,
                ExtractedPath     = extractedPath,
                BrokenTransitions = brokenTransitions,
                FinalVerdict      = finalVerdict
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // BƯỚC 1: Semantic-to-Node Mapping – BATCH EMBEDDING (1 lần gọi API)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gom toàn bộ văn bản cần embed (question + tất cả node descriptions +
        /// tất cả sliding-window chunks) vào 1 danh sách, gọi Gemini Embedding API
        /// duy nhất 1 lần, rồi map ngược vector về từng node/chunk bằng CPU.
        ///
        /// Luồng:
        ///   1. Build textBatch = [question, node0, node1, …, nodeN, chunk0, chunk1, …, chunkM]
        ///   2. Gọi EmbedBatchWithRetryAsync(textBatch) → vectors[]
        ///   3. vectors[0]         = questionVector
        ///      vectors[1..N]      = nodeVectors
        ///      vectors[N+1..N+M]  = chunkVectors
        ///   4. Tính cosine similarity bằng CPU; lọc theo SimilarityThreshold
        ///   5. Chọn MatchedPhrase cho từng node thắng bằng cosine với chunkVectors (CPU)
        ///   6. Sắp xếp theo vị trí chunk trong câu hỏi
        /// </summary>
        private async Task<List<ExtractedPathStepDto>> Step1_EmbeddingMappingAsync(
            string question,
            List<WorkflowNodeInputDto> nodes)
        {
            if (nodes.Count == 0)
                return new List<ExtractedPathStepDto>();

            // ── Chuẩn bị danh sách text cần embed ────────────────────────────
            var nodeTexts = nodes.Select(n =>
            {
                var t = $"{n.NodeName}. {n.Description}".Trim();
                return string.IsNullOrWhiteSpace(t) ? n.NodeId : t;
            }).ToList();

            var questionChunks = BuildSlidingWindowChunks(question, windowSize: 5, stepSize: 2);
            var chunkTexts = questionChunks.Select(c => c.Chunk).ToList();

            // ── Gom vào 1 batch duy nhất: [question] + [nodes] + [chunks] ────
            // Bố cục: index 0 = question, 1..N = nodes, N+1..N+M = chunks
            var textBatch = new List<string>(1 + nodeTexts.Count + chunkTexts.Count);
            textBatch.Add(question);          // idx 0
            textBatch.AddRange(nodeTexts);    // idx 1 … N
            textBatch.AddRange(chunkTexts);   // idx N+1 … N+M

            _logger.LogInformation(
                "[Bước 1] Batch embed {Total} texts (1 question + {N} nodes + {M} chunks) trong 1 request.",
                textBatch.Count, nodeTexts.Count, chunkTexts.Count);

            // ── Gọi API 1 lần duy nhất ───────────────────────────────────────
            var allVectors = await EmbedBatchWithRetryAsync(textBatch);

            // ── Map ngược vector theo index ───────────────────────────────────
            var questionVector = allVectors[0];
            var nodeVectors    = allVectors.Skip(1).Take(nodeTexts.Count).ToArray();
            var chunkVectors   = allVectors.Skip(1 + nodeTexts.Count).ToArray();

            // ── Tính cosine similarity + lọc theo ngưỡng (hoàn toàn bằng CPU) ─
            var candidates = new List<(WorkflowNodeInputDto Node, double Similarity, string MatchedPhrase, int PhrasePosition)>();

            for (int i = 0; i < nodes.Count; i++)
            {
                double sim = CosineSimilarity(questionVector, nodeVectors[i]);
                if (sim < SimilarityThreshold) continue;

                // Tìm chunk gần nhất với node này (CPU-only cosine)
                string bestPhrase = questionChunks.Count > 0 ? questionChunks[0].Chunk : question;
                int    bestPos    = questionChunks.Count > 0 ? questionChunks[0].StartPos : 0;
                double bestChunkSim = double.MinValue;

                for (int j = 0; j < questionChunks.Count && j < chunkVectors.Length; j++)
                {
                    double chunkSim = CosineSimilarity(nodeVectors[i], chunkVectors[j]);
                    if (chunkSim > bestChunkSim)
                    {
                        bestChunkSim = chunkSim;
                        bestPhrase   = questionChunks[j].Chunk;
                        bestPos      = questionChunks[j].StartPos;
                    }
                }

                candidates.Add((nodes[i], sim, bestPhrase, bestPos));

                _logger.LogDebug("[Bước 1] Node '{Name}' | sim={Sim:F3} | phrase='{P}'",
                    nodes[i].NodeName, sim, bestPhrase);
            }

            // ── Sắp xếp theo thứ tự tiến trình thời gian trong câu hỏi ───────
            var orderedCandidates = candidates
                .OrderBy(c => c.PhrasePosition)
                .ThenByDescending(c => c.Similarity)
                .ToList();

            return orderedCandidates
                .Select((c, idx) => new ExtractedPathStepDto
                {
                    Step            = idx + 1,
                    NodeId          = c.Node.NodeId,
                    NodeName        = c.Node.NodeName,
                    MatchedPhrase   = c.MatchedPhrase,
                    SimilarityScore = Math.Round(c.Similarity, 4)
                })
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // BƯỚC 2: Graph Connection Verification (Path Alignment)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Duyệt chuỗi extractedPath từ đầu đến cuối.
        /// Với mỗi cặp (Node[i], Node[i+1]): tra cứu EdgeSet O(1).
        /// Nếu không tồn tại cạnh nối → đánh dấu BrokenTransition.
        /// </summary>
        private static (bool IsAccurate, List<BrokenTransitionDto> BrokenTransitions)
            Step2_GraphConnectionVerification(
                List<ExtractedPathStepDto>  extractedPath,
                List<WorkflowNodeInputDto>  nodes,
                List<WorkflowEdgeInputDto>  edges)
        {
            var brokenTransitions = new List<BrokenTransitionDto>();

            if (extractedPath.Count < 2)
                return (true, brokenTransitions); // Ít hơn 2 nút → không cần kiểm tra cạnh

            // ── 2a. Xây dựng EdgeSet O(1) từ danh sách cạnh ────────────────
            // Key = (FromNodeId, ToNodeId) đã lowercase/trim để tránh mismatch
            var edgeSet = new HashSet<(string From, string To)>(
                edges
                    .Where(e => !string.IsNullOrWhiteSpace(e.FromNodeId)
                             && !string.IsNullOrWhiteSpace(e.ToNodeId))
                    .Select(e => (
                        e.FromNodeId.Trim().ToLowerInvariant(),
                        e.ToNodeId.Trim().ToLowerInvariant()
                    ))
            );

            // Lookup: NodeId → NodeName (để điền tên đầy đủ vào BrokenTransition)
            var nodeLookup = nodes.ToDictionary(
                n => n.NodeId.Trim().ToLowerInvariant(),
                n => n.NodeName,
                StringComparer.OrdinalIgnoreCase);

            // ── 2b. Duyệt từng cặp liên tiếp ───────────────────────────────
            for (int i = 0; i < extractedPath.Count - 1; i++)
            {
                var fromStep = extractedPath[i];
                var toStep   = extractedPath[i + 1];

                var fromKey = fromStep.NodeId.Trim().ToLowerInvariant();
                var toKey   = toStep.NodeId.Trim().ToLowerInvariant();

                if (!edgeSet.Contains((fromKey, toKey)))
                {
                    // Cạnh không tồn tại → đứt gãy luồng
                    brokenTransitions.Add(new BrokenTransitionDto
                    {
                        FromNode = fromStep.NodeName,
                        ToNode   = toStep.NodeName,
                        Reason   = $"Không tồn tại chuyển tiếp trực tiếp từ \"{fromStep.NodeName}\" " +
                                   $"sang \"{toStep.NodeName}\" trong đồ thị luồng nghiệp vụ. " +
                                   $"Câu hỏi đang mô tả một bước nhảy cóc hoặc đi ngược quy trình hệ thống."
                    });
                }
            }

            bool isAccurate = brokenTransitions.Count == 0;
            return (isAccurate, brokenTransitions);
        }

        // ─────────────────────────────────────────────────────────────────────
        // FINAL VERDICT: Sinh lời luận tội / chứng minh bằng Gemini text
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi Gemini text model để tổng hợp bằng chứng và trả về một đoạn văn
        /// phân tích tính chính xác của câu hỏi đối với Workflow.
        /// </summary>
        private async Task<string> GenerateFinalVerdictAsync(
            string question,
            string workflowName,
            List<ExtractedPathStepDto>   extractedPath,
            List<BrokenTransitionDto>    brokenTransitions,
            bool isAccurate)
        {
            // ── Xây dựng prompt phân tích ──────────────────────────────────
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là chuyên gia kiểm định quy trình nghiệp vụ. Hãy viết một đoạn phân tích ngắn gọn (3-5 câu) bằng tiếng Việt về tính chính xác của câu hỏi sau đây đối với luồng nghiệp vụ.");
            sb.AppendLine();
            sb.AppendLine($"**Câu hỏi cần đánh giá:**");
            sb.AppendLine($"\"{question}\"");
            sb.AppendLine();
            sb.AppendLine($"**Workflow:** {workflowName}");
            sb.AppendLine();

            sb.AppendLine("**Chuỗi bước được trích xuất từ câu hỏi (theo thứ tự):**");
            if (extractedPath.Count > 0)
            {
                foreach (var step in extractedPath)
                {
                    sb.AppendLine($"  Bước {step.Step}: [{step.NodeId}] {step.NodeName}" +
                                  $" (khớp với cụm \"{step.MatchedPhrase}\", sim={step.SimilarityScore:F3})");
                }
            }
            else
            {
                sb.AppendLine("  (Không tìm được nút nào tương đồng trong workflow)");
            }
            sb.AppendLine();

            if (brokenTransitions.Count > 0)
            {
                sb.AppendLine("**Các chuyển tiếp bị đứt gãy:**");
                foreach (var bt in brokenTransitions)
                {
                    sb.AppendLine($"  ✗ \"{bt.FromNode}\" → \"{bt.ToNode}\": {bt.Reason}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"**Kết luận sơ bộ:** Câu hỏi {(isAccurate ? "CHÍNH XÁC" : "KHÔNG CHÍNH XÁC")} về mặt luồng nghiệp vụ.");
            sb.AppendLine();
            sb.AppendLine("Hãy đưa ra phân tích chi tiết, chỉ rõ tại sao câu hỏi phản ánh đúng hoặc sai luồng hệ thống, " +
                          "và nêu rủi ro nghiệp vụ nếu có. Chỉ trả về đoạn văn phân tích, không thêm tiêu đề hay định dạng markdown.");

            // ── Gọi Gemini với retry ───────────────────────────────────────
            string verdict = string.Empty;
            int retries    = 3;
            int delay      = 3;

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cohereApiKey);
                    
                    var payload = new
                    {
                        model = VerdictModel,
                        message = sb.ToString(),
                        temperature = 0.4
                    };

                    var response = await client.PostAsync(
                        "https://api.cohere.com/v1/chat",
                        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("text", out var textProp))
                        {
                            verdict = textProp.GetString()?.Trim() ?? string.Empty;
                            break;
                        }
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                    {
                        if (attempt == retries) throw new InvalidOperationException("Cohere Rate Limit Exceeded");
                        
                        int waitSeconds = delay;
                        if (response.Headers.RetryAfter?.Delta.HasValue == true)
                        {
                            waitSeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds + 1;
                        }

                        _logger.LogWarning("[FinalVerdict] Rate limit 429. Thử lại sau {D}s... (Attempt {A}/{R})", 
                            waitSeconds, attempt, retries);
                        
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                        delay = Math.Max(delay * 2, 3);
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Cohere Chat API Error: {Error}", error);
                        throw new InvalidOperationException($"Cohere Chat API Error ({response.StatusCode}): {error}");
                    }
                }
                catch (Exception ex) when (attempt < retries && ex.Message.Contains("Rate Limit"))
                {
                    // Đã xử lý ở trên
                }
            }

            // Fallback nếu Gemini không trả lời được
            if (string.IsNullOrWhiteSpace(verdict))
            {
                verdict = isAccurate
                    ? $"Câu hỏi phản ánh đúng luồng nghiệp vụ của workflow '{workflowName}'. " +
                      $"Toàn bộ {extractedPath.Count} bước được trích xuất đều liên thông hợp lệ."
                    : $"Câu hỏi KHÔNG phản ánh đúng luồng nghiệp vụ của workflow '{workflowName}'. " +
                      $"Phát hiện {brokenTransitions.Count} chuyển tiếp bị đứt gãy.";
            }

            return verdict;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS: Gemini Embedding (Batch)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi Gemini Embedding API với danh sách văn bản (batch) duy nhất 1 lần.
        /// Trả về mảng double[][] – mỗi phần tử tương ứng với 1 văn bản đầu vào.
        /// Có cơ chế retry với exponential backoff cho rate-limit.
        /// </summary>
        private async Task<double[][]> EmbedBatchWithRetryAsync(List<string> texts)
        {
            if (texts.Count == 0)
                return Array.Empty<double[]>();

            const int batchSize = 96; // Cohere limit is 96 texts per embed request
            var allEmbeddings = new List<double[]>();

            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var chunkTexts = texts.Skip(i).Take(batchSize).ToList();
                
                int retries = 3;
                int delay   = 3;
                bool success = false;

                for (int attempt = 1; attempt <= retries; attempt++)
                {
                    try
                    {
                        var client = _httpClientFactory.CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cohereApiKey);
                        
                        var payload = new
                        {
                            texts = chunkTexts,
                            model = EmbeddingModel,
                            input_type = "search_document"
                        };

                        var response = await client.PostAsync(
                            "https://api.cohere.com/v1/embed",
                            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("embeddings", out var embeddingsProp))
                            {
                                var embeddings = embeddingsProp.EnumerateArray()
                                    .Select(e => e.EnumerateArray().Select(v => v.GetDouble()).ToArray())
                                    .ToList();
                                
                                allEmbeddings.AddRange(embeddings);
                                success = true;
                                break;
                            }
                        }
                        else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                        {
                            if (attempt == retries) throw new InvalidOperationException("Cohere Rate Limit Exceeded");
                            
                            int waitSeconds = delay;
                            if (response.Headers.RetryAfter?.Delta.HasValue == true)
                            {
                                waitSeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds + 1;
                            }

                            _logger.LogWarning("[EmbedBatch] Rate limit 429. Thử lại sau {D}s... (Attempt {A}/{R})", 
                                waitSeconds, attempt, retries);
                            
                            await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                            delay = Math.Max(delay * 2, 3);
                        }
                        else
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            _logger.LogError("Cohere Embed API Error: {Error}", error);
                            throw new InvalidOperationException($"Cohere Embed API Error ({response.StatusCode}): {error}");
                        }
                    }
                    catch (Exception ex) when (attempt < retries && ex.Message.Contains("Rate Limit"))
                    {
                        // Đã xử lý ở trên
                    }
                }

                if (!success)
                {
                    throw new InvalidOperationException("Không thể lấy batch embedding từ Cohere sau nhiều lần thử.");
                }

                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(200); // Cohere is faster, slight delay is enough
                }
            }

            return allEmbeddings.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS: Toán học và xử lý văn bản
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tính cosine similarity giữa 2 vector.
        /// Kết quả trong [-1, 1]; với embedding văn bản thường trong [0, 1].
        /// </summary>
        private static double CosineSimilarity(double[] a, double[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Hai vector phải có cùng số chiều.");

            double dot    = 0.0;
            double normA  = 0.0;
            double normB  = 0.0;

            for (int i = 0; i < a.Length; i++)
            {
                dot   += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denom < 1e-10 ? 0.0 : dot / denom;
        }

        /// <summary>
        /// Tạo các cụm từ sliding window từ một đoạn văn bản.
        /// Ví dụ: "A B C D E" với window=3, step=1 → ["A B C", "B C D", "C D E"]
        /// Mỗi cụm được kèm theo vị trí bắt đầu (character index) trong văn bản gốc.
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
