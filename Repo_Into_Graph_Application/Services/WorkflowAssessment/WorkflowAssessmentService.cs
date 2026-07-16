using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_Application.Services.AI;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    /// <summary>
    /// Triển khai Pipeline 3 bước đánh giá Câu hỏi nghiệp vụ dựa trên Đồ thị luồng.
    ///
    /// ┌──────────────────────────────────────────────────────────────────────────┐
    /// │  Bước 1 – Semantic Vector Mapping (Gemini text-embedding-004 THỰC)       │
    /// │    • Embed Question → vector Q                                            │
    /// │    • Embed Description+Keywords mỗi nút → vector N_i                     │
    /// │    • Cosine similarity(Q, N_i) → chọn nút có sim > threshold             │
    /// │    • Sắp xếp theo vị trí xuất hiện của cụm từ khớp trong câu hỏi        │
    /// │                                                                            │
    /// │  Bước 2 – Path Matching / Alignment                                       │
    /// │    • Kiểm tra mỗi cặp (Node[i], Node[i+1]) trong EdgeSet O(1)            │
    /// │                                                                            │
    /// │  Bước 3 – Metrics Calculation (Coverage + Difficulty)                     │
    /// │    • CoverageWorkflowOverGlobal, CoverageActiveOverWorkflow               │
    /// │    • ImpactPathLength, GatewaysCount, DifficultyLevel                     │
    /// └──────────────────────────────────────────────────────────────────────────┘
    ///
    /// Ngoài ra, hỗ trợ <see cref="AssessCoverageAsync"/> để tự động
    /// query DB xây dựng đồ thị từ <see cref="GenerateQuestionsResponse"/>.
    /// </summary>
    public class WorkflowAssessmentService : IWorkflowAssessmentService
    {
        private const double SimilarityThreshold = 0.50;
        private const string EmbeddingModel = "embed-multilingual-v3.0";

        private readonly ILogger<WorkflowAssessmentService> _logger;
        private readonly IBusinessRepository _businessRepository;
        private readonly IFeatureBusinessMappingRepository _featureBusinessMappingRepository;
        private readonly IFeatureMethodMappingRepository _featureMethodMappingRepository;
        private readonly IEmbeddingService _embeddingService;
        private readonly IAccuracyAssessmentService _accuracyAssessmentService;
        private readonly IDifficultyAssessmentService _difficultyAssessmentService;
        private readonly IMethodSourceRepository _methodSourceRepository;

        public WorkflowAssessmentService(
            ILogger<WorkflowAssessmentService> logger,
            IBusinessRepository businessRepository,
            IFeatureBusinessMappingRepository featureBusinessMappingRepository,
            IFeatureMethodMappingRepository featureMethodMappingRepository,
            IEmbeddingService embeddingService,
            IAccuracyAssessmentService accuracyAssessmentService,
            IDifficultyAssessmentService difficultyAssessmentService,
            IMethodSourceRepository methodSourceRepository)
        {
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
            _businessRepository = businessRepository
                ?? throw new ArgumentNullException(nameof(businessRepository));
            _featureBusinessMappingRepository = featureBusinessMappingRepository
                ?? throw new ArgumentNullException(nameof(featureBusinessMappingRepository));
            _featureMethodMappingRepository = featureMethodMappingRepository
                ?? throw new ArgumentNullException(nameof(featureMethodMappingRepository));
            _accuracyAssessmentService = accuracyAssessmentService
                ?? throw new ArgumentNullException(nameof(accuracyAssessmentService));
            _difficultyAssessmentService = difficultyAssessmentService
                ?? throw new ArgumentNullException(nameof(difficultyAssessmentService));
            _methodSourceRepository = methodSourceRepository
                ?? throw new ArgumentNullException(nameof(methodSourceRepository));

            _embeddingService = embeddingService
                ?? throw new ArgumentNullException(nameof(embeddingService));
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC – Entry point thủ công (truyền graph sẵn)
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<AssessmentResultDto> AssessAsync(AssessmentRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            if (string.IsNullOrWhiteSpace(request.Question))
                throw new ArgumentException("Câu hỏi (Question) không được để trống.", nameof(request));
            if (request.SelectedWorkflow is null)
                throw new ArgumentException("SelectedWorkflow không được null.", nameof(request));
            if (request.GlobalGraph is null)
                throw new ArgumentException("GlobalGraph không được null.", nameof(request));

            _logger.LogInformation("[WorkflowAssessment] Đánh giá câu hỏi cho Workflow '{WorkflowName}'.",
                request.SelectedWorkflow.WorkflowName);

            return await RunPipelineAsync(request.Question, request.SelectedWorkflow, request.GlobalGraph);
        }

        public async Task<CoverageAssessmentResultDto> AssessCoverageAsync(GenerateQuestionsResponse response)
        {
            ArgumentNullException.ThrowIfNull(response, nameof(response));

            if (response.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId không được rỗng.", nameof(response));

            var questions = (response.GeneratedQuestionDtos ?? Enumerable.Empty<GeneratedQuestionDto>()).ToList();
            if (questions.Count == 0)
            {
                _logger.LogWarning("[AssessFromResponse] BusinessId={Id}: không có câu hỏi nào.", response.BusinessId);
                return new CoverageAssessmentResultDto
                {
                    BusinessId = response.BusinessId,
                    BusinessName = response.BusinessName,
                    TotalQuestions = 0
                };
            }

            _logger.LogInformation("[AssessFromResponse] Business '{Name}' – {Count} câu hỏi.",
                response.BusinessName, questions.Count);

            // ── Phase A: Xây dựng đồ thị từ DB ───────────────────────────────
            var (workflowGraph, globalGraph) = await BuildGraphsFromDbAsync(response.BusinessId);

            _logger.LogInformation("[AssessFromResponse] Đồ thị: {W} nút Workflow | {G} nút Global",
                workflowGraph.Nodes.Count, globalGraph.AllNodes.Count);

            // ── Phase B: Chạy Pipeline cho từng câu hỏi ──────────────────────
            var questionResults = new List<QuestionCoverageDto>(questions.Count);

            foreach (var q in questions)
            {
                if (string.IsNullOrWhiteSpace(q.Question))
                {
                    _logger.LogWarning("[AssessFromResponse] Bỏ qua câu hỏi rỗng.");
                    continue;
                }

                AssessmentResultDto? graphResult = null;
                try
                {
                    graphResult = await RunPipelineAsync(q.Question, workflowGraph, globalGraph);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AssessFromResponse] Lỗi đánh giá câu hỏi: '{Q}'", q.Question);
                }

                int activeNodesCount = graphResult?.ActiveNodeIds.Count ?? 0;

                questionResults.Add(new QuestionCoverageDto
                {
                    Question = q.Question,
                    ActiveNodeCount = activeNodesCount,
                    Coverage = graphResult?.TotalCoverage ?? 0.0,
                    CoverageWorkflowOverGlobal = graphResult?.CoverageWorkflowOverGlobal ?? 0.0,
                    CoverageActiveOverWorkflow = graphResult?.CoverageActiveOverWorkflow ?? 0.0
                });
            }

            // ── Phase C: Thống kê tổng hợp ────────────────────────────────────
            int total = questionResults.Count;
            double avgCoverage = total > 0
                ? questionResults.Select(r => r.Coverage).DefaultIfEmpty(0.0).Average()
                : 0.0;

            return new CoverageAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName,
                TotalQuestions = total,
                AverageTotalCoverage = avgCoverage,
                WorkflowNodeCount = workflowGraph.Nodes.Count,
                GlobalNodeCount = globalGraph.AllNodes.Count,
                QuestionResults = questionResults
            };
        }

        /// <inheritdoc/>
        public async Task<BusinessWorkflowGraphDto> GetBusinessWorkflowGraphAsync(Guid businessId)
        {
            var (workflowGraph, globalGraph) = await BuildGraphsFromDbAsync(businessId);

            return new BusinessWorkflowGraphDto
            {
                BusinessId = businessId,
                BusinessName = workflowGraph.WorkflowName,
                WorkflowNodeCount = workflowGraph.Nodes.Count,
                GlobalNodeCount = globalGraph.AllNodes.Count,
                Nodes = workflowGraph.Nodes.Select(n => new BusinessWorkflowNodeDto
                {
                    Id = n.Id,
                    Name = n.Name,
                    Type = n.Type.ToString(),
                    Description = n.Description
                }).ToList(),
                Edges = workflowGraph.Edges.Select(e => new BusinessWorkflowEdgeDto
                {
                    FromNodeId = e.FromNodeId,
                    ToNodeId = e.ToNodeId,
                    Condition = e.Label
                }).ToList()
            };
        }

        /// <inheritdoc/>
        public async Task<WorkflowDataDto> GetWorkflowDataAsync(Guid businessId)
        {
            var (workflowGraph, _) = await BuildGraphsFromDbAsync(businessId);

            return new WorkflowDataDto
            {
                WorkflowName = workflowGraph.WorkflowName,
                Nodes = workflowGraph.Nodes.Select(n => new WorkflowNodeInputDto
                {
                    NodeId = n.Id,
                    NodeName = n.Name,
                    Description = n.Description,
                    SourceCode = n.SourceCode
                }).ToList(),
                Edges = workflowGraph.Edges.Select(e => new WorkflowEdgeInputDto
                {
                    FromNodeId = e.FromNodeId,
                    ToNodeId = e.ToNodeId
                }).ToList()
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE: Xây dựng đồ thị từ DB theo BusinessId
        // ─────────────────────────────────────────────────────────────────────

        private async Task<(WorkflowGraphDto WorkflowGraph, GlobalGraphDto GlobalGraph)>
            BuildGraphsFromDbAsync(Guid businessId)
        {
            // A1. Feature IDs của Business
            var featureIds = await _featureBusinessMappingRepository
                .GetFeatureIdsByBusinessIdAsync(businessId);

            // A2. FeatureMethodMappings + MethodSource
            var featureMappings = featureIds.Count > 0
                ? await _featureMethodMappingRepository
                    .GetMappingsWithMethodSourceByFeatureIdsAsync(featureIds)
                : new List<Repo_Into_Graph_DataAccess.Models.Feature.FeatureMethodMapping>();

            // A3. Workflow Nodes — mỗi MethodSource là 1 nút
            var workflowMethods = featureMappings
                .Where(m => m.MethodSource != null)
                .Select(m => m.MethodSource!)
                .DistinctBy(m => m.Id)
                .ToList();

            var workflowNodes = workflowMethods.Select(m =>
            {
                var kws = new List<string> { m.ClassName.Trim(), m.MethodName.Trim() };
                kws.AddRange(SplitCamelCase(m.MethodName));
                kws.AddRange(SplitCamelCase(m.ClassName));
                return new NodeDto
                {
                    Id = m.Id.ToString(),
                    Name = $"{m.ClassName}.{m.MethodName}",
                    Type = InferNodeType(m.MethodName),
                    // Description đầy đủ để Gemini Embedding có đủ ngữ cảnh ngữ nghĩa
                    Description = $"{m.ClassName} {m.MethodName} " +
                                  $"{string.Join(" ", kws)} " +
                                  $"{ExtractKeywordsFromSource(m.SourceCode)}",
                    Keywords = kws,
                    SourceCode = m.SourceCode ?? string.Empty
                };
            }).ToList();

            // A4. Workflow Edges — nối tuần tự Method trong cùng Feature
            var workflowEdges = new List<EdgeDto>();
            var methodsByFeature = featureMappings
                .Where(m => m.MethodSource != null)
                .GroupBy(m => m.FeatureId)
                .ToList();

            foreach (var featureGroup in methodsByFeature)
            {
                var methods = featureGroup
                    .Select(m => m.MethodSource!)
                    .DistinctBy(m => m.Id)
                    .ToList();

                for (int i = 0; i < methods.Count - 1; i++)
                {
                    workflowEdges.Add(new EdgeDto
                    {
                        FromNodeId = methods[i].Id.ToString(),
                        ToNodeId = methods[i + 1].Id.ToString()
                    });
                }
            }

            var globalNodes = new List<NodeDto>();
            if (workflowMethods.Count > 0)
            {
                var runIds = workflowMethods.Select(m => m.AnalysisRunId).Distinct().ToList();
                var allMethodsInRun = await _methodSourceRepository
                    .FindAsync(m => runIds.Contains(m.AnalysisRunId));

                globalNodes = allMethodsInRun
                    .Select(m =>
                    {
                        var kws = new List<string> { m.ClassName.Trim(), m.MethodName.Trim() };
                        kws.AddRange(SplitCamelCase(m.MethodName));
                        kws.AddRange(SplitCamelCase(m.ClassName));
                        return new NodeDto
                        {
                            Id = m.Id.ToString(),
                            Name = $"{m.ClassName}.{m.MethodName}",
                            Type = InferNodeType(m.MethodName),
                            Description = $"{m.ClassName} {m.MethodName}",
                            Keywords = kws
                        };
                    })
                    .ToList();
            }

            var workflowGraph = new WorkflowGraphDto
            {
                WorkflowId = businessId.ToString(),
                WorkflowName = $"Business_{businessId}",
                Nodes = workflowNodes,
                Edges = workflowEdges
            };

            var globalGraph = new GlobalGraphDto
            {
                AllNodes = globalNodes,
                AllEdges = new List<EdgeDto>()
            };

            return (workflowGraph, globalGraph);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE: Pipeline 3 bước (dùng chung cho cả 2 entry point)
        // ─────────────────────────────────────────────────────────────────────

        private async Task<AssessmentResultDto> RunPipelineAsync(
            string question,
            WorkflowGraphDto workflowGraph,
            GlobalGraphDto globalGraph)
        {
            var result = new AssessmentResultDto();

            // Bước 1: Semantic Mapping (Gemini Embedding thực)
            var activeNodes = await Step1_EmbeddingMappingAsync(question, workflowGraph.Nodes);
            result.ActiveNodeIds = activeNodes.Select(n => n.Id).ToList();
            result.ActiveNodes = activeNodes;

            // Bước 2: Metrics (Coverage)
            Step2_CalculateMetrics(activeNodes, workflowGraph.Nodes, globalGraph.AllNodes, result);

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BƯỚC 1: Semantic Mapping — Gemini text-embedding-004 THỰC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sử dụng Gemini Embedding API để tính cosine similarity giữa Câu hỏi
        /// và Description+Keywords của từng nút.
        ///
        /// Quy trình BATCH (1 request duy nhất):
        ///   1. Gom [question] + [nodeText_0, …, nodeText_N] vào textBatch
        ///   2. Gọi EmbedBatchWithRetryAsync(textBatch) → vectors[]
        ///   3. vectors[0] = questionVector; vectors[1..N] = nodeVectors
        ///   4. Tính cosine similarity(Q, N_i) bằng CPU; giữ nút có sim > SimilarityThreshold
        ///   5. Sắp xếp theo similarity giảm dần (nút liên quan nhất trước)
        /// </summary>
        private async Task<List<NodeDto>> Step1_EmbeddingMappingAsync(
            string question,
            List<NodeDto> workflowNodes)
        {
            if (workflowNodes == null || workflowNodes.Count == 0)
                return new List<NodeDto>();

            // Nhúng Câu hỏi (với input_type = search_query)
            var qVectors = await _embeddingService.EmbedBatchAsync(new List<string> { question }, "search_query");
            var questionVector = qVectors[0];

            // Gom văn bản cần embed cho các nút (với input_type = search_document)
            var nodeTexts = workflowNodes.Select(BuildNodeText).ToList();

            _logger.LogInformation(
                "[Bước 1] Gọi Cohere Embed: 1 search_query và {N} search_document.", nodeTexts.Count);

            var nodeVectors = await _embeddingService.EmbedBatchAsync(nodeTexts, "search_document");

            // Tính cosine similarity hoàn toàn bằng CPU
            var scoredNodes = new List<(NodeDto Node, double Similarity)>(workflowNodes.Count);
            for (int i = 0; i < workflowNodes.Count; i++)
            {
                double sim = _embeddingService.CosineSimilarity(questionVector, nodeVectors[i]);

                _logger.LogInformation("[Bước 1] Node '{Name}' | sim={Sim:F4}", workflowNodes[i].Name, sim);

                if (sim >= SimilarityThreshold)
                    scoredNodes.Add((workflowNodes[i], sim));
            }

            // Sắp xếp theo similarity giảm dần (nút liên quan nhất → đứng đầu)
            return scoredNodes
                .OrderByDescending(x => x.Similarity)
                .Select(x => x.Node)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // BƯỚC 2: Metrics — Coverage
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Coverage_Luong_Graph  = |WorkflowNodes| / |GlobalNodes|
        /// Coverage_CauHoi_Luong = |ActiveNodes|   / |WorkflowNodes|
        /// TotalCoverage         = Coverage_Luong_Graph × Coverage_CauHoi_Luong
        /// </summary>
        private void Step2_CalculateMetrics(
            List<NodeDto> activeNodes,
            List<NodeDto> workflowNodes,
            List<NodeDto> globalNodes,
            AssessmentResultDto result)
        {
            int activeCount = activeNodes?.Count ?? 0;
            int workflowCount = workflowNodes?.Count ?? 0;
            int globalCount = globalNodes?.Count ?? 0;

            double coverageWG = globalCount > 0 ? (double)workflowCount / globalCount : 0.0;
            double coverageAW = workflowCount > 0 ? (double)activeCount / workflowCount : 0.0;

            result.CoverageWorkflowOverGlobal = coverageWG;
            result.CoverageActiveOverWorkflow = coverageAW;
            result.TotalCoverage = coverageWG * coverageAW;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GENERAL HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ghép Name + Description + Keywords của nút thành văn bản đại diện
        /// ngữ nghĩa đầy đủ để embed.
        /// </summary>
        private static string BuildNodeText(NodeDto node)
        {
            var sb = new StringBuilder();
            sb.Append(node.Name);
            if (!string.IsNullOrWhiteSpace(node.Description))
            {
                sb.Append(". ");
                sb.Append(node.Description);
            }

            if (node.Keywords is { Count: > 0 })
            {
                sb.Append(". ");
                sb.Append(string.Join(" ", node.Keywords));
            }

            return sb.ToString().Trim();
        }

        private static string ClassifyDifficulty(int gateways, int pathLength)
        {
            if (gateways >= 2 || pathLength >= 3) return "Khó";
            if (gateways == 1) return "Trung bình";
            return "Dễ";
        }

        private static NodeType InferNodeType(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return NodeType.Activity;
            var lower = methodName.ToLowerInvariant();

            if (lower.StartsWith("check") || lower.StartsWith("validate") ||
                lower.StartsWith("should") || lower.StartsWith("has") ||
                lower.StartsWith("is") || lower.StartsWith("can") ||
                lower.Contains("decision") || lower.Contains("gateway"))
                return NodeType.DecisionGateway;

            return NodeType.Activity;
        }



        private static string ExtractKeywordsFromSource(string sourceCode)
        {
            if (string.IsNullOrWhiteSpace(sourceCode)) return string.Empty;
            return sourceCode.Length > 300 ? sourceCode[..300] : sourceCode;
        }

        private static IEnumerable<string> SplitCamelCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) yield break;
            var current = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsUpper(c) && current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                current.Append(c);
            }
            if (current.Length > 0) yield return current.ToString();
        }
    }
}
