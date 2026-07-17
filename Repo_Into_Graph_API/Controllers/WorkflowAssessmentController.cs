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

        /// <summary>
        /// [POST] Tính Độ bao phủ (Coverage) từ GenerateQuestionsResponse.
        /// Query DB xây đồ thị Workflow, chạy Semantic Mapping + Path Matching + Metrics
        /// cho từng câu hỏi và trả về BatchAssessmentResultDto.
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

        [HttpPost("assess-difficulty")]
        public async Task<IActionResult> AssessDifficulty([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            // 1. Tự động lấy cấu trúc đồ thị luồng nghiệp vụ của Business từ DB
            var workflowData = await _assessmentService.GetWorkflowDataAsync(response.BusinessId);
            var workflowGraph = await _assessmentService.GetBusinessWorkflowGraphAsync(response.BusinessId);

            // 2. Chạy pipeline assess-accuracy để tìm ActiveNodes (ExtractedPath)
            var accuracyBatchResult = await _accuracyAssessmentService.AssessAccuracyBatchAsync(response, workflowData);

            // 3. Khởi tạo kết quả batch cho Độ Khó
            var batchDifficultyResult = new BatchDifficultyAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName,
                QuestionResults = new List<QuestionDifficultyAssessmentResultDto>()
            };

            // 4. Lặp qua từng câu hỏi đã được xác thực
            foreach (var qResult in accuracyBatchResult.QuestionResults)
            {
                // Map ExtractedPath sang GraphNodeDto kèm theo NodeType
                var activeNodes = qResult.AccuracyResult.ExtractedPath.Select(p => 
                {
                    var graphNode = workflowGraph.Nodes.FirstOrDefault(n => n.Id == p.NodeId);
                    return new GraphNodeDto
                    {
                        NodeId = p.NodeId,
                        NodeName = p.NodeName,
                        NodeType = graphNode?.Type ?? "Activity"
                    };
                }).ToList();

                var diffRequest = new DifficultyAssessmentRequestDto
                {
                    ActiveNodes = activeNodes,
                    // Nếu không có đồ thị con phức tạp, ta fallback E_q bằng số cạnh tuyến tính
                    TotalEdgesInSubgraph = Math.Max(0, activeNodes.Count - 1) 
                };

                var diffResult = await _difficultyAssessmentService.AssessAsync(diffRequest);

                batchDifficultyResult.QuestionResults.Add(new QuestionDifficultyAssessmentResultDto
                {
                    Question = qResult.Question,
                    DifficultyResult = diffResult
                });
            }

            return Ok(batchDifficultyResult);
        }



    }
}

