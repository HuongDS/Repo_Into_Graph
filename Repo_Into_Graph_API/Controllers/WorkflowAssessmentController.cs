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

            var batchResult = await _accuracyAssessmentService.AssessAccuracyBatchAsync(response, workflowData);

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



    }
}

