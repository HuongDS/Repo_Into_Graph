using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.AI;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    public class SemanticMappingHelper : ISemanticMappingHelper
    {
        private const double BaseSimilarityThreshold = 0.50;

        // ─────────────────────────────────────────────────────────────────────
        // ĐỐI CHIẾU TargetedEntryPoints VỚI NODE CỦA ĐỒ THỊ
        //
        // Bản cũ dùng Contains() HAI CHIỀU:
        //     t.Contains(n.NodeName) || n.NodeName.Contains(t)
        // Với các tên có quan hệ tiền tố, nó khớp bừa:
        //     t = "DatasetController.getAllDatasetType"
        //     n = "DatasetController.getAllDataset"      -> t.Contains(n) = TRUE
        // Nên mọi câu hỏi về getAllDatasetType hay getAllDatasetParent đều bị chèn
        // thêm node getAllDataset vào đường đi. Node thừa này không có cạnh nối với
        // các node còn lại => brokenTransitions => isAccurate = false.
        //
        // Hậu quả đo được (nghiệp vụ "Danh mục & Loại Dataset", 9 câu):
        //   - 7/9 câu có node lạ chen vào đầu đường đi
        //   - 2 câu DUY NHẤT đạt 25/25 là 2 câu về searchByDatasetName — cái tên
        //     duy nhất không phải tiền tố của tên nào khác
        // Tức tỷ lệ chính xác đang phản ánh VA CHẠM TÊN, không phải chất lượng câu hỏi.
        //
        // Bản mới: chỉ khớp TUYỆT ĐỐI. Ưu tiên khớp đủ "Class.Method"; nếu không có
        // node nào khớp đủ thì mới khớp tuyệt đối phần tên method (để xử lý trường hợp
        // LLM khai tên interface "IDatasetService.getX" trong khi đồ thị lưu lớp cài đặt
        // "DatasetServiceImpl.getX"). Không bao giờ dùng so khớp chuỗi con.
        //
        // Thứ tự trả về bám theo thứ tự LLM khai (call stack), không phải thứ tự node
        // trong đồ thị — bản cũ dùng nodes.Where() nên trả về theo thứ tự đồ thị, làm
        // đảo ngược đường đi (ServiceImpl đứng trước Controller).
        // ─────────────────────────────────────────────────────────────────────
        private static string NormalizeNodeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var v = s.Trim();

            // LLM đôi khi viết kèm tham số: "getAllDataset()" hoặc "getAll(String name)"
            int paren = v.IndexOf('(');
            if (paren >= 0) v = v.Substring(0, paren);

            return v.Trim().ToLowerInvariant();
        }

        private static string MethodSegment(string normalized)
        {
            int dot = normalized.LastIndexOf('.');
            return dot >= 0 && dot < normalized.Length - 1
                ? normalized.Substring(dot + 1)
                : normalized;
        }

        private static List<WorkflowNodeInputDto> MatchTargetedEntryPoints(
            string[] targetedEntryPoints,
            List<WorkflowNodeInputDto> nodes)
        {
            var result = new List<WorkflowNodeInputDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in targetedEntryPoints)
            {
                var t = NormalizeNodeName(raw);
                if (t.Length == 0) continue;

                // 1. Khớp tuyệt đối cả "Class.Method"
                var hits = nodes
                    .Where(n => NormalizeNodeName(n.NodeName) == t)
                    .ToList();

                // 2. Không có thì khớp tuyệt đối phần TÊN METHOD (interface <-> impl)
                if (hits.Count == 0)
                {
                    var tm = MethodSegment(t);
                    if (tm.Length > 0)
                    {
                        hits = nodes
                            .Where(n => MethodSegment(NormalizeNodeName(n.NodeName)) == tm)
                            .ToList();
                    }
                }

                foreach (var n in hits)
                {
                    var key = NormalizeNodeName(n.NodeName);
                    if (key.Length > 0 && seen.Add(key)) result.Add(n);
                }
            }

            return result;
        }

        private readonly IEmbeddingService _embeddingService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<SemanticMappingHelper> _logger;

        public SemanticMappingHelper(
            IEmbeddingService embeddingService,
            IDistributedCache cache,
            ILogger<SemanticMappingHelper> logger)
        {
            _embeddingService = embeddingService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<ExtractedPathStepDto>> GetSemanticMappingAsync(
            Guid businessId,
            string question,
            List<WorkflowNodeInputDto> nodes,
            double[][]? precomputedNodeVectors = null,
            string[]? targetedEntryPoints = null)
        {
            if (nodes.Count == 0)
                return new List<ExtractedPathStepDto>();

            // Nếu đã biết TargetedEntryPoints, bypass vector search!
            if (targetedEntryPoints != null && targetedEntryPoints.Length > 0)
            {
                var matchedNodes = MatchTargetedEntryPoints(targetedEntryPoints, nodes);

                if (matchedNodes.Count > 0)
                {
                    _logger.LogInformation("[GetSemanticMappingAsync] Bypass Vector Search, sử dụng {C} TargetedEntryPoints.", matchedNodes.Count);
                    return matchedNodes.Select((n, idx) => new ExtractedPathStepDto
                    {
                        Step = idx + 1,
                        NodeId = n.NodeId,
                        NodeName = n.NodeName,
                        MatchedPhrase = "Exact Match from TargetedEntryPoints",
                        SimilarityScore = 1.0
                    }).ToList();
                }
                else
                {
                    _logger.LogWarning("[GetSemanticMappingAsync] Cảnh báo: Có {C} TargetedEntryPoints nhưng không Node nào khớp tên (VD: {T})! Chuyển sang Vector Search.", targetedEntryPoints.Length, targetedEntryPoints[0]);
                }
            }

            string cacheKey = $"active_nodes_{businessId}_{question.GetHashCode()}";
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("[GetSemanticMappingAsync] Đã lấy ActiveNodes từ Cache cho câu hỏi.");
                return JsonSerializer.Deserialize<List<ExtractedPathStepDto>>(cachedData)!;
            }

            var questionChunks = BuildSlidingWindowChunks(question, windowSize: 5, stepSize: 2);
            var chunkTexts = questionChunks.Select(c => c.Chunk).ToList();

            // Nhúng Câu hỏi và chunk (với input_type = search_query)
            var queryBatch = new List<string>(1 + chunkTexts.Count);
            queryBatch.Add(question);
            queryBatch.AddRange(chunkTexts);
            
            var queryVectors = await _embeddingService.EmbedBatchAsync(queryBatch, "search_query");
            var questionVector = queryVectors[0];
            var chunkVectors = queryVectors.Skip(1).ToArray();

            // Nhúng Nodes (với input_type = search_document)
            var nodeTexts = nodes.Select(n => $"{n.NodeName}. {n.Description}".Trim()).ToList();
            
            _logger.LogInformation(
                "[Bước 1] Gọi Cohere Embed: {Q} search_query và {N} search_document.", queryBatch.Count, nodeTexts.Count);

            var nodeVectors = precomputedNodeVectors ?? await _embeddingService.EmbedBatchAsync(nodeTexts, "search_document");

            // ── Tính cosine similarity + lọc theo ngưỡng động (Dynamic Threshold) ─
            var allSims = new List<(WorkflowNodeInputDto Node, double Similarity, double[] NodeVector)>();
            double maxSim = double.MinValue;
            
            for (int i = 0; i < nodes.Count; i++)
            {
                double sim = _embeddingService.CosineSimilarity(questionVector, nodeVectors[i]);
                allSims.Add((nodes[i], sim, nodeVectors[i]));
                if (sim > maxSim) maxSim = sim;
            }

            double dynamicThreshold = Math.Max(BaseSimilarityThreshold, maxSim * 0.95);
            _logger.LogInformation("[GetSemanticMappingAsync] MaxScore={Max:F4}, DynamicThreshold={Thresh:F4}", maxSim, dynamicThreshold);

            var candidates = new List<(WorkflowNodeInputDto Node, double Similarity, string MatchedPhrase, int PhrasePosition)>();

            foreach (var item in allSims)
            {
                if (item.Similarity < dynamicThreshold) continue;

                // Tìm chunk gần nhất với node này (CPU-only cosine)
                string bestPhrase = questionChunks.Count > 0 ? questionChunks[0].Chunk : question;
                int    bestPos    = questionChunks.Count > 0 ? questionChunks[0].StartPos : 0;
                double bestChunkSim = double.MinValue;

                for (int j = 0; j < questionChunks.Count && j < chunkVectors.Length; j++)
                {
                    double chunkSim = _embeddingService.CosineSimilarity(item.NodeVector, chunkVectors[j]);
                    if (chunkSim > bestChunkSim)
                    {
                        bestChunkSim = chunkSim;
                        bestPhrase   = questionChunks[j].Chunk;
                        bestPos      = questionChunks[j].StartPos;
                    }
                }

                candidates.Add((item.Node, item.Similarity, bestPhrase, bestPos));

                _logger.LogDebug("[Bước 1] Node '{Name}' | sim={Sim:F3} | phrase='{P}'",
                    item.Node.NodeName, item.Similarity, bestPhrase);
            }

            // ── Loại bỏ trùng lặp: Mỗi cụm từ (PhrasePosition) chỉ lấy 1 Node liên quan nhất ──
            var filteredCandidates = candidates
                .GroupBy(c => c.PhrasePosition)
                .Select(g => g.OrderByDescending(c => c.Similarity).First())
                .ToList();

            // ── Sắp xếp theo thứ tự tiến trình thời gian trong câu hỏi ───────
            var orderedCandidates = filteredCandidates
                .OrderBy(c => c.PhrasePosition)
                .ToList();

            var finalPath = orderedCandidates
                .Select((c, idx) => new ExtractedPathStepDto
                {
                    Step = idx + 1,
                    NodeId = c.Node.NodeId,
                    NodeName = c.Node.NodeName,
                    MatchedPhrase = c.MatchedPhrase,
                    SimilarityScore = Math.Round(c.Similarity, 4)
                })
                .ToList();

            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60) };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalPath), cacheOptions);
            return finalPath;
        }

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
