using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowAssessmentController : ControllerBase
    {
        private readonly IWorkflowAssessmentService _assessmentService;
        private readonly IAccuracyAssessmentService _accuracyAssessmentService;
        private readonly IDifficultyAssessmentService _difficultyAssessmentService;

        public WorkflowAssessmentController(
            IWorkflowAssessmentService assessmentService,
            IAccuracyAssessmentService accuracyAssessmentService,
            IDifficultyAssessmentService difficultyAssessmentService)
        {
            _assessmentService = assessmentService
                ?? throw new ArgumentNullException(nameof(assessmentService));
            _accuracyAssessmentService = accuracyAssessmentService
                ?? throw new ArgumentNullException(nameof(accuracyAssessmentService));
            _difficultyAssessmentService = difficultyAssessmentService
                ?? throw new ArgumentNullException(nameof(difficultyAssessmentService));
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/workflowassessment/assess-from-response   ← CHỈ ĐỘ BAO PHỦ
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [POST] Tính Độ bao phủ (Coverage) từ GenerateQuestionsResponse.
        /// Query DB xây đồ thị Workflow, chạy Semantic Mapping + Path Matching + Metrics
        /// cho từng câu hỏi và trả về BatchAssessmentResultDto.
        /// <para>
        /// Để đánh giá toàn diện (Coverage + Accuracy + Difficulty), dùng
        /// <c>POST /api/WorkflowAssessment/assess-all</c>.
        /// </para>
        /// </summary>
        [HttpPost("assess-from-response")]
        public async Task<IActionResult> AssessFromResponse([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            var result = await _assessmentService.AssessCoverageAsync(response);
            return Ok(result);
        }

        /// <summary>
        /// [POST] Đánh giá tính chính xác (Accuracy) của danh sách câu hỏi trong GenerateQuestionsResponse
        /// bằng cách tự động truy vấn cấu trúc đồ thị từ database.
        ///
        /// Trả về BatchAccuracyAssessmentResultDto chứa kết quả đánh giá chi tiết (extracted path,
        /// broken transitions, final verdict) của từng câu hỏi.
        /// </summary>
        [HttpPost("assess-accuracy")]
        public async Task<IActionResult> AssessAccuracy([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            // Tự động lấy cấu trúc đồ thị luồng nghiệp vụ của Business từ DB
            var workflowData = await _assessmentService.GetWorkflowDataAsync(response.BusinessId);

            var batchResult = new BatchAccuracyAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName
            };

            foreach (var q in response.GeneratedQuestionDtos ?? Enumerable.Empty<GeneratedQuestionDto>())
            {
                if (string.IsNullOrWhiteSpace(q.Question)) continue;

                var accuracyRequest = new AccuracyAssessmentRequestDto
                {
                    Question = q.Question,
                    WorkflowData = workflowData
                };

                var accuracyResult = await _accuracyAssessmentService.AssessAccuracyAsync(accuracyRequest);

                batchResult.QuestionResults.Add(new QuestionAccuracyAssessmentResultDto
                {
                    Question = q.Question,
                    AccuracyResult = accuracyResult
                });
            }

            return Ok(batchResult);
        }

        /// <summary>
        /// [POST] Tính toán và chứng minh Độ Khó (Difficulty) của câu hỏi
        /// dựa trên tập hợp Active Nodes đã được xác thực ở bước assess-accuracy.
        ///
        /// Logic thuần toán học, không cần Gemini API hay Database:
        ///   - V(G) = E_q - V_q + 2 (Cyclomatic Complexity – McCabe)
        ///   - L_q  = ActiveNodes.Count - 1 (Impact Path Length)
        ///   - GatewaysCount = số nút loại DecisionGateway
        ///   - PathType: Happy Path | Single Exception | Double Exception
        ///   - Level: Dễ | Trung bình | Khó
        ///
        /// Trả về DifficultyAssessmentResultDto:
        ///   - level, cyclomatic_complexity, impact_path_length, gateways_count, path_type, reasoning
        /// </summary>
        [HttpPost("assess-difficulty")]
        public async Task<IActionResult> AssessDifficulty([FromBody] DifficultyAssessmentRequestDto request)
        {
            if (request == null)
                throw new BadRequestException("Request body không được để trống.");

            if (request.ActiveNodes == null || request.ActiveNodes.Count == 0)
                throw new BadRequestException("Trường 'activeNodes' không được rỗng. " +
                    "Hãy truyền vào danh sách nút đã được xác thực từ bước assess-accuracy.");

            var result = await _difficultyAssessmentService.AssessAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// [POST] Orchestrator tổng hợp — đánh giá TOÀN BỘ câu hỏi trong GenerateQuestionsResponse
        /// theo Pipeline 3 bước đầy đủ:
        ///
        /// <list type="number">
        ///   <item>
        ///     <b>Bước 1 – Coverage:</b> Gọi <c>AssessFromResponseAsync</c> — query DB,
        ///     xây đồ thị Workflow, chạy Semantic Mapping + Path Matching + Metrics
        ///     cho từng câu hỏi.
        ///   </item>
        ///   <item>
        ///     <b>Bước 2 – Accuracy (Short-Circuit):</b> Trích xuất kết quả tính chính xác
        ///     từ Bước 1. Nếu câu hỏi bị đứt gãy luồng (<c>IsAccurate = false</c>),
        ///     bỏ qua Bước 3 cho câu đó và đặt <c>Difficulty = null</c>.
        ///   </item>
        ///   <item>
        ///     <b>Bước 3 – Difficulty:</b> Với câu hỏi hợp lệ, gọi
        ///     <c>DifficultyAssessmentService</c> để tính V(G), L_q, PathType và
        ///     Reasoning đầy đủ từ danh sách Active Nodes đã xác thực.
        ///   </item>
        /// </list>
        ///
        /// Input: GenerateQuestionsResponse (output từ QuestionGeneratorController).<br/>
        /// Output: <see cref="ComprehensiveAssessmentResponse"/> chứa Coverage + per-question Accuracy + Difficulty.
        /// </summary>
        [HttpPost("assess-all")]
        public async Task<IActionResult> AssessAll([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            // ══════════════════════════════════════════════════════════════════
            // BƯỚC 1: Coverage
            // AssessFromResponseAsync query DB, xây đồ thị và chạy toàn bộ
            // Semantic Mapping + Path Matching + Metrics cho từng câu hỏi.
            // ══════════════════════════════════════════════════════════════════
            var coverageResult = await _assessmentService.AssessCoverageAsync(response);

            // Map Coverage → ComprehensiveCoverageDto
            var coverageDto = new ComprehensiveCoverageDto
            {
                BusinessId = coverageResult.BusinessId,
                BusinessName = coverageResult.BusinessName,
                TotalQuestions = coverageResult.TotalQuestions,
                AccurateCount = coverageResult.AccurateCount,
                AccuracyRate = coverageResult.AccuracyRate,
                AverageTotalCoverage = coverageResult.AverageTotalCoverage,
                WorkflowNodeCount = coverageResult.WorkflowNodeCount,
                GlobalNodeCount = coverageResult.GlobalNodeCount
            };

            var questionAssessments = new List<ComprehensiveQuestionResultDto>();
            int totalAccurate = 0;
            int totalInaccurate = 0;
            var difficultyBucket = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var qResult in coverageResult.QuestionResults)
            {
                var questionDto = new ComprehensiveQuestionResultDto
                {
                    Question = qResult.Question,
                    SuggestedAnswer = qResult.SuggestedAnswer,
                    AiDifficulty = qResult.AiDifficulty
                };

                var graphAssessment = qResult.GraphAssessment;

                if (graphAssessment == null)
                {
                    questionDto.Accuracy = new ComprehensiveAccuracyDto { IsAccurate = false };
                    questionDto.Difficulty = null;
                    totalInaccurate++;
                    questionAssessments.Add(questionDto);
                    continue;
                }

                // ════════════════════════════════════════════════════════════
                // BƯỚC 2: Accuracy — trích xuất từ kết quả Bước 1
                // (đã tính nội bộ trong AssessFromResponseAsync, không gọi lại API)
                // ════════════════════════════════════════════════════════════
                questionDto.Accuracy = new ComprehensiveAccuracyDto
                {
                    IsAccurate = graphAssessment.IsAccurate,
                    ActiveNodeIds = graphAssessment.ActiveNodeIds,
                    ActiveNodes = graphAssessment.ActiveNodes
                        .Select(n => new ActiveNodeSummaryDto
                        {
                            Id = n.Id,
                            Name = n.Name,
                            Type = n.Type.ToString()
                        })
                        .ToList(),
                    BrokenLinks = graphAssessment.BrokenLinks
                        .Select(bl => new BrokenLinkDto { FromId = bl.FromId, ToId = bl.ToId })
                        .ToList(),
                    TotalCoverage = graphAssessment.TotalCoverage,
                    CoverageWorkflowOverGlobal = graphAssessment.CoverageWorkflowOverGlobal,
                    CoverageActiveOverWorkflow = graphAssessment.CoverageActiveOverWorkflow,
                    SubgraphEdgeCount = graphAssessment.SubgraphEdgeCount
                };

                // SHORT-CIRCUIT: luồng đứt gãy → bỏ qua Bước 3
                if (!graphAssessment.IsAccurate)
                {
                    questionDto.Difficulty = null;
                    totalInaccurate++;
                    questionAssessments.Add(questionDto);
                    continue;
                }

                // ════════════════════════════════════════════════════════════
                // BƯỚC 3: Difficulty — gọi DifficultyAssessmentService
                // Lấy V(G), PathType, Reasoning đầy đủ từ Active Nodes đã xác thực
                // ════════════════════════════════════════════════════════════
                var activeGraphNodes = graphAssessment.ActiveNodes
                    .Select(n => new GraphNodeDto
                    {
                        NodeId = n.Id,
                        NodeName = n.Name,
                        NodeType = n.Type == NodeType.DecisionGateway
                            ? "DecisionGateway"
                            : "Activity"
                    })
                    .ToList();

                var difficultyResult = await _difficultyAssessmentService.AssessAsync(
                    new DifficultyAssessmentRequestDto
                    {
                        ActiveNodes = activeGraphNodes,
                        TotalEdgesInSubgraph = graphAssessment.SubgraphEdgeCount
                    });

                questionDto.Difficulty = difficultyResult;

                difficultyBucket.TryGetValue(difficultyResult.Level, out int cnt);
                difficultyBucket[difficultyResult.Level] = cnt + 1;

                totalAccurate++;
                questionAssessments.Add(questionDto);
            }

            // Chuỗi phân phối độ khó
            string difficultyDistribution = difficultyBucket.Count > 0
                ? string.Join(", ", difficultyBucket.OrderBy(k => k.Key).Select(k => $"{k.Key}: {k.Value}"))
                : "N/A";

            bool overallSuccess = totalInaccurate == 0;
            string message = overallSuccess
                ? $"Đánh giá toàn diện hoàn thành. Toàn bộ {totalAccurate} câu hỏi đều chính xác."
                : $"Phát hiện {totalInaccurate}/{coverageResult.TotalQuestions} câu hỏi đứt gãy luồng. " +
                  "Bước 3 (Độ Khó) đã bị bỏ qua cho các câu hỏi không chính xác.";

            return Ok(new ComprehensiveAssessmentResponse
            {
                IsSuccess = overallSuccess,
                Message = message,
                Coverage = coverageDto,
                QuestionAssessments = questionAssessments,
                TotalAccurate = totalAccurate,
                TotalInaccurate = totalInaccurate,
                DifficultyDistribution = difficultyDistribution
            });
        }

        [HttpPost("assess")]
        public async Task<IActionResult> Assess([FromBody] AssessmentRequestDto request)
        {
            if (request == null)
                throw new BadRequestException("Request body không được để trống.");

            if (string.IsNullOrWhiteSpace(request.Question))
                throw new BadRequestException("Trường 'question' không được để trống.");

            var result = await _assessmentService.AssessAsync(request);
            return Ok(result);
        }
    }
}

