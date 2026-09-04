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

        public WorkflowAssessmentController(IWorkflowAssessmentService assessmentService)
        {
            _assessmentService = assessmentService
                ?? throw new ArgumentNullException(nameof(assessmentService));
        }

        /// <summary>
        /// [POST] Tính Độ bao phủ (Coverage) từ danh sách câu hỏi.
        /// </summary>
        [HttpPost("assess-from-response")]
        public async Task<IActionResult> AssessFromResponse([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            var graphs = await _assessmentService.BuildGraphsFromDbAsync(response.BusinessId);
            var result = await _assessmentService.Coverage.AssessCoverageBatchAsync(response, graphs.WorkflowGraph, graphs.GlobalGraph);
            return Ok(result);
        }

        /// <summary>
        /// [POST] Đánh giá tính chính xác (Accuracy) của danh sách câu hỏi.
        /// </summary>
        [HttpPost("assess-accuracy")]
        public async Task<IActionResult> AssessAccuracy([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            // Lấy đồ thị nghiệp vụ từ DB
            var workflowData = await _assessmentService.GetWorkflowDataAsync(response.BusinessId);

            var batchResult = await _assessmentService.Accuracy.AssessAccuracyBatchAsync(response, workflowData);

            return Ok(batchResult);
        }

        [HttpPost("assess-difficulty")]
        public async Task<IActionResult> AssessDifficulty([FromBody] GenerateQuestionsResponse response)
        {
            if (response == null)
                throw new BadRequestException("Request body không được để trống.");

            if (response.BusinessId == Guid.Empty)
                throw new BadRequestException("Trường 'businessId' không được rỗng.");

            // 1. Lấy đồ thị nghiệp vụ từ DB
            var businessWorkflowGraph = await _assessmentService.GetBusinessWorkflowGraphAsync(response.BusinessId);
            var workflowGraph = businessWorkflowGraph.Nodes;

            var workflowData = new WorkflowDataDto
            {
                WorkflowName = businessWorkflowGraph.BusinessName,
                Nodes = businessWorkflowGraph.Nodes.Select(n => new WorkflowNodeInputDto
                {
                    NodeId = n.Id,
                    NodeName = n.Name,
                    Description = n.Description,
                    SourceCode = ""
                }).ToList(),
                Edges = businessWorkflowGraph.Edges.Select(e => new WorkflowEdgeInputDto
                {
                    FromNodeId = e.FromNodeId,
                    ToNodeId = e.ToNodeId,
                    Condition = e.Condition
                }).ToList()
            };

            // 2. Lấy ActiveNodes từ pipeline AssessAccuracy
            var accuracyBatchResult = await _assessmentService.Accuracy.AssessAccuracyBatchAsync(response, workflowData);

            // 3. Khởi tạo kết quả
            var batchDifficultyResult = new BatchDifficultyAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName,
                QuestionResults = new List<QuestionDifficultyAssessmentResultDto>()
            };

            // 4. Đánh giá từng câu hỏi
            foreach (var qResult in accuracyBatchResult.QuestionResults)
            {
                var activeNodes = qResult.AccuracyResult.ExtractedPath.Select(p =>
                {
                    var graphNode = workflowGraph.FirstOrDefault(n => n.Id == p.NodeId);
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
                    TotalEdgesInSubgraph = Math.Max(0, activeNodes.Count - 1)
                };

                var diffResult = await _assessmentService.Difficulty.AssessAsync(diffRequest);

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
