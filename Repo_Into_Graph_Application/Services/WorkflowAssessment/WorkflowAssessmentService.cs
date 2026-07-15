using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_DataAccess.Repository.Interface;

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
        // ── Hằng số Embedding ─────────────────────────────────────────────────
        /// <summary>
        /// Ngưỡng cosine similarity tối thiểu để một nút được coi là "active".
        /// Giá trị 0.50 cho phép bắt các nút liên quan gián tiếp.
        /// </summary>
        private const double SimilarityThreshold = 0.50;

        private const string EmbeddingModel = "embed-multilingual-v3.0";

        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly ILogger<WorkflowAssessmentService> _logger;
        private readonly IBusinessRepository _businessRepository;
        private readonly IFeatureBusinessMappingRepository _featureBusinessMappingRepository;
        private readonly IFeatureMethodMappingRepository _featureMethodMappingRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _cohereApiKey;
        private readonly IAccuracyAssessmentService _accuracyAssessmentService;
        private readonly IDifficultyAssessmentService _difficultyAssessmentService;
        private readonly IMethodSourceRepository _methodSourceRepository;

        public WorkflowAssessmentService(
            ILogger<WorkflowAssessmentService> logger,
            IBusinessRepository businessRepository,
            IFeatureBusinessMappingRepository featureBusinessMappingRepository,
            IFeatureMethodMappingRepository featureMethodMappingRepository,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
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

            _httpClientFactory = httpClientFactory 
                ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _cohereApiKey = (configuration ?? throw new ArgumentNullException(nameof(configuration)))
                ["CohereConfig:ApiKey"]
                ?? throw new InvalidOperationException("Thiếu cấu hình CohereConfig:ApiKey.");
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

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC – Auto-query từ DB theo GenerateQuestionsResponse
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<BatchAssessmentResultDto> AssessCoverageAsync(GenerateQuestionsResponse response)
        {
            ArgumentNullException.ThrowIfNull(response, nameof(response));

            if (response.BusinessId == Guid.Empty)
                throw new ArgumentException("BusinessId không được rỗng.", nameof(response));

            var questions = (response.GeneratedQuestionDtos ?? Enumerable.Empty<GeneratedQuestionDto>()).ToList();
            if (questions.Count == 0)
            {
                _logger.LogWarning("[AssessFromResponse] BusinessId={Id}: không có câu hỏi nào.", response.BusinessId);
                return new BatchAssessmentResultDto
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
            var questionResults = new List<QuestionAssessmentResultDto>(questions.Count);

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

                questionResults.Add(new QuestionAssessmentResultDto
                {
                    Question = q.Question,
                    SuggestedAnswer = q.SuggestedAnswer,
                    AiDifficulty = q.Difficulty,
                    GraphAssessment = graphResult,
                    Coverage = graphResult?.TotalCoverage ?? 0.0,
                    CoverageWorkflowOverGlobal = graphResult?.CoverageWorkflowOverGlobal ?? 0.0,
                    CoverageActiveOverWorkflow = graphResult?.CoverageActiveOverWorkflow ?? 0.0
                });
            }

            // ── Phase C: Thống kê tổng hợp ────────────────────────────────────
            int total = questionResults.Count;
            int accurate = questionResults.Count(r => r.GraphAssessment?.IsAccurate == true);
            double accuracyRate = total > 0 ? (double)accurate / total : 0.0;
            double avgCoverage = total > 0
                ? questionResults
                    .Where(r => r.GraphAssessment != null)
                    .Select(r => r.GraphAssessment!.TotalCoverage)
                    .DefaultIfEmpty(0.0)
                    .Average()
                : 0.0;

            return new BatchAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName,
                TotalQuestions = total,
                AccurateCount = accurate,
                AccuracyRate = accuracyRate,
                AverageTotalCoverage = avgCoverage,
                WorkflowNodeCount = workflowGraph.Nodes.Count,
                GlobalNodeCount = globalGraph.AllNodes.Count,
                QuestionResults = questionResults
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC – Orchestrator tổng hợp: Accuracy + Difficulty trong 1 lần gọi
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<AssessAllResultDto> AssessAllAsync(AssessAllRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            if (string.IsNullOrWhiteSpace(request.Question))
                throw new ArgumentException("Câu hỏi (Question) không được để trống.", nameof(request));
            if (request.WorkflowData is null)
                throw new ArgumentException("WorkflowData không được null.", nameof(request));

            var nodes = request.WorkflowData.Nodes ?? new List<WorkflowNodeInputDto>();
            var edges = request.WorkflowData.Edges ?? new List<WorkflowEdgeInputDto>();

            _logger.LogInformation(
                "[AssessAll] Bắt đầu – Workflow='{W}' | {N} nút | {E} cạnh | Q='{Q}'",
                request.WorkflowData.WorkflowName, nodes.Count, edges.Count,
                request.Question.Length > 80 ? request.Question[..80] + "…" : request.Question);

            // ── Phase 1: Accuracy Assessment (Bước 1 Semantic Mapping + Bước 2 Path Matching)
            var accuracyRequest = new AccuracyAssessmentRequestDto
            {
                Question = request.Question,
                WorkflowData = request.WorkflowData
            };

            var accuracyResult = await _accuracyAssessmentService.AssessAccuracyAsync(accuracyRequest);

            _logger.LogInformation(
                "[AssessAll] Accuracy done – IsAccurate={A} | {P} nút active",
                accuracyResult.IsAccurate, accuracyResult.ExtractedPath.Count);

            // ── Phase 2: Difficulty Assessment (Bước 3 Metrics)
            // Map ExtractedPath → List<GraphNodeDto> với NodeType được suy ra từ tên nút.
            var activeGraphNodes = accuracyResult.ExtractedPath
                .Select(step => new GraphNodeDto
                {
                    NodeId = step.NodeId,
                    NodeName = step.NodeName,
                    NodeType = InferNodeTypeString(step.NodeName)
                })
                .ToList();

            // Tính số cạnh kết nối giữa các Active Nodes trong đồ thị con G_q
            var activeNodeIds = accuracyResult.ExtractedPath
                .Select(s => s.NodeId.Trim().ToLowerInvariant())
                .ToHashSet();

            int subgraphEdges = edges.Count(e =>
                activeNodeIds.Contains(e.FromNodeId?.Trim().ToLowerInvariant() ?? string.Empty) &&
                activeNodeIds.Contains(e.ToNodeId?.Trim().ToLowerInvariant() ?? string.Empty));

            var difficultyRequest = new DifficultyAssessmentRequestDto
            {
                ActiveNodes = activeGraphNodes,
                TotalEdgesInSubgraph = subgraphEdges
            };

            var difficultyResult = await _difficultyAssessmentService.AssessAsync(difficultyRequest);

            _logger.LogInformation(
                "[AssessAll] Difficulty done – Level={L} | V(G)={VG} | L_q={Lq}",
                difficultyResult.Level, difficultyResult.CyclomaticComplexity, difficultyResult.ImpactPathLength);

            // ── Phase 3: Gộp kết quả
            return new AssessAllResultDto
            {
                Question = request.Question,
                WorkflowName = request.WorkflowData.WorkflowName,
                AccuracyResult = accuracyResult,
                DifficultyResult = difficultyResult,
                ActiveNodeCount = accuracyResult.ExtractedPath.Count,
                WorkflowNodeCount = nodes.Count,
                WorkflowEdgeCount = edges.Count
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
                    Description = n.Description
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
                    Keywords = kws
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

            // A5. Global Nodes — toàn bộ method name trong cùng AnalysisRun của hệ thống
            var globalNodes = new List<NodeDto>();
            if (workflowMethods.Count > 0)
            {
                var analysisRunId = workflowMethods.First().AnalysisRunId;
                var allMethodsInRun = await _methodSourceRepository
                    .FindAsync(m => m.AnalysisRunId == analysisRunId);

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

            // Bước 2: Path Matching
            var (isAccurate, brokenLinks) = Step2_PathMatching(activeNodes, workflowGraph.Edges);
            result.IsAccurate = isAccurate;
            result.BrokenLinks = brokenLinks;

            // Tính SubgraphEdgeCount: số cạnh E_q kết nối giữa các Active Nodes trong G_q
            // Dùng để tính Cyclomatic Complexity V(G) = E_q - V_q + 2
            var activeNodeIdSet = activeNodes
                .Select(n => n.Id.Trim().ToLowerInvariant())
                .ToHashSet();

            result.SubgraphEdgeCount = (workflowGraph.Edges ?? Enumerable.Empty<EdgeDto>())
                .Count(e =>
                    activeNodeIdSet.Contains(e.FromNodeId?.Trim().ToLowerInvariant() ?? string.Empty) &&
                    activeNodeIdSet.Contains(e.ToNodeId?.Trim().ToLowerInvariant() ?? string.Empty));

            // Bước 3: Metrics (Coverage + Difficulty cơ bản)
            Step3_CalculateMetrics(activeNodes, workflowGraph.Nodes, globalGraph.AllNodes, result);

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

            // Gom văn bản cần embed: question đầu tiên, sau đó từng node
            var nodeTexts = workflowNodes.Select(BuildNodeText).ToList();

            var textBatch = new List<string>(1 + nodeTexts.Count);
            textBatch.Add(question);        // idx 0
            textBatch.AddRange(nodeTexts);  // idx 1 … N

            _logger.LogInformation(
                "[Bước 1] Batch embed {Total} texts (1 question + {N} nodes) trong 1 request.",
                textBatch.Count, nodeTexts.Count);

            // Gọi API 1 lần duy nhất
            var allVectors = await EmbedBatchWithRetryAsync(textBatch);

            var questionVector = allVectors[0];

            // Tính cosine similarity hoàn toàn bằng CPU
            var scoredNodes = new List<(NodeDto Node, double Similarity)>(workflowNodes.Count);
            for (int i = 0; i < workflowNodes.Count; i++)
            {
                double sim = CosineSimilarity(questionVector, allVectors[i + 1]);

                _logger.LogDebug("[Bước 1] Node '{Name}' | sim={Sim:F4}", workflowNodes[i].Name, sim);

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
        // BƯỚC 2: Path Matching — kiểm tra liên thông của chuỗi Active Nodes
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Với mỗi cặp (Node[i], Node[i+1]) kiểm tra cạnh trong EdgeSet O(1).
        /// </summary>
        private (bool IsAccurate, List<(string FromId, string ToId)> BrokenLinks)
            Step2_PathMatching(List<NodeDto> activeNodes, List<EdgeDto> workflowEdges)
        {
            var brokenLinks = new List<(string, string)>();

            if (activeNodes == null || activeNodes.Count < 2)
                return (true, brokenLinks);

            var edgeSet = new HashSet<(string From, string To)>(
                (workflowEdges ?? Enumerable.Empty<EdgeDto>())
                    .Where(e => !string.IsNullOrWhiteSpace(e.FromNodeId)
                             && !string.IsNullOrWhiteSpace(e.ToNodeId))
                    .Select(e => (e.FromNodeId.Trim(), e.ToNodeId.Trim()))
            );

            for (int i = 0; i < activeNodes.Count - 1; i++)
            {
                var from = activeNodes[i].Id;
                var to = activeNodes[i + 1].Id;

                if (!edgeSet.Contains((from, to)))
                {
                    brokenLinks.Add((from, to));
                    _logger.LogWarning("[Bước 2] Đứt gãy: [{From}] → [{To}].", from, to);
                }
            }

            return (brokenLinks.Count == 0, brokenLinks);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BƯỚC 3: Metrics — Coverage & Difficulty
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Coverage_Luong_Graph  = |WorkflowNodes| / |GlobalNodes|
        /// Coverage_CauHoi_Luong = |ActiveNodes|   / |WorkflowNodes|
        /// TotalCoverage         = Coverage_Luong_Graph × Coverage_CauHoi_Luong
        ///
        /// Lưu ý: CodeCoverage từ GenerateQuestionsResponse bị bỏ qua
        /// vì chỉ số đó không chính xác.
        /// </summary>
        private void Step3_CalculateMetrics(
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

            result.ImpactPathLength = Math.Max(0, activeCount - 1);

            result.GatewaysCount = (activeNodes ?? Enumerable.Empty<NodeDto>())
                .Count(n => n.Type == NodeType.DecisionGateway);

            result.DifficultyLevel = ClassifyDifficulty(result.GatewaysCount, result.ImpactPathLength);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GEMINI EMBEDDING HELPERS (Batch)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gọi Gemini Embedding API với danh sách văn bản (batch) duy nhất 1 lần.
        /// Trả về mảng double[][] – mỗi phần tử tương ứng với 1 văn bản đầu vào.
        /// Có cơ chế retry exponential backoff cho rate-limit.
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
                int delay = 3;
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
                            "https://api.cohere.com/v2/embed",
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
                    await Task.Delay(200);
                }
            }

            return allEmbeddings.ToArray();
        }

        /// <summary>
        /// Tính cosine similarity giữa 2 vector.
        /// Kết quả trong [0, 1] với embedding văn bản.
        /// </summary>
        private static double CosineSimilarity(double[] a, double[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Hai vector phải cùng số chiều.");

            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denom < 1e-10 ? 0.0 : dot / denom;
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

        /// <summary>
        /// Biến thể trả về string "DecisionGateway" / "Activity" —
        /// dùng trong <see cref="AssessAllAsync"/> khi map ExtractedPath sang
        /// <see cref="GraphNodeDto.NodeType"/>.
        /// </summary>
        private static string InferNodeTypeString(string nodeName)
            => InferNodeType(nodeName) == NodeType.DecisionGateway
                ? "DecisionGateway"
                : "Activity";

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
