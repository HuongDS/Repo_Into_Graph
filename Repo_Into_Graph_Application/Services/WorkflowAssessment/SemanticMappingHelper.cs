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
                var matchedNodes = nodes.Where(n =>
                    !string.IsNullOrWhiteSpace(n.NodeName) && targetedEntryPoints.Any(t =>
                        !string.IsNullOrWhiteSpace(t) &&
                        (string.Equals(t.Trim(), n.NodeName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                         t.Trim().Contains(n.NodeName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                         n.NodeName.Trim().Contains(t.Trim(), StringComparison.OrdinalIgnoreCase))
                    )
                ).ToList();

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
