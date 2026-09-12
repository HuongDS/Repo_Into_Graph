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

                // ── Đếm CẠNH THẬT của đồ thị con cảm sinh G_q ──────────────────────
                // Trước đây dòng này là: TotalEdgesInSubgraph = activeNodes.Count - 1
                // Thay E_q = V_q - 1 vào V(G) = E_q - V_q + 2 sẽ ra V(G) = 1 với MỌI dữ liệu
                // (đẳng thức đại số) -> chỉ số mất sạch phương sai. Nay đếm cạnh thật.
                var activeIds = activeNodes
                    .Select(n => n.NodeId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet();

                int realEdges = businessWorkflowGraph.Edges.Count(e =>
                    !string.IsNullOrWhiteSpace(e.FromNodeId) &&
                    !string.IsNullOrWhiteSpace(e.ToNodeId) &&
                    activeIds.Contains(e.FromNodeId) &&
                    activeIds.Contains(e.ToNodeId));

                int components = CountConnectedComponents(activeIds, businessWorkflowGraph.Edges);

                var diffRequest = new DifficultyAssessmentRequestDto
                {
                    ActiveNodes = activeNodes,
                    TotalEdgesInSubgraph = realEdges,
                    ConnectedComponents = components
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

        /// <summary>
        /// Đếm số thành phần liên thông (P) của đồ thị con cảm sinh trên tập nút
        /// <paramref name="nodeIds"/>, phục vụ công thức McCabe đầy đủ V(G) = E - N + 2P.
        /// Cạnh được xem là VÔ HƯỚNG khi xét tính liên thông (hướng không ảnh hưởng tới P).
        /// </summary>
        private static int CountConnectedComponents(
            HashSet<string> nodeIds,
            List<BusinessWorkflowEdgeDto> edges)
        {
            if (nodeIds == null || nodeIds.Count == 0) return 0;

            // Danh sách kề vô hướng, chỉ giữ cạnh nằm TRỌN trong tập nút
            var adj = nodeIds.ToDictionary(id => id, _ => new List<string>());
            foreach (var e in edges ?? new List<BusinessWorkflowEdgeDto>())
            {
                if (string.IsNullOrWhiteSpace(e.FromNodeId) || string.IsNullOrWhiteSpace(e.ToNodeId)) continue;
                if (!nodeIds.Contains(e.FromNodeId) || !nodeIds.Contains(e.ToNodeId)) continue;
                if (e.FromNodeId == e.ToNodeId) continue;   // self-loop không ảnh hưởng tính liên thông
                adj[e.FromNodeId].Add(e.ToNodeId);
                adj[e.ToNodeId].Add(e.FromNodeId);
            }

            var visited = new HashSet<string>();
            int components = 0;

            foreach (var start in nodeIds)
            {
                if (!visited.Add(start)) continue;
                components++;

                var stack = new Stack<string>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    foreach (var next in adj[current])
                    {
                        if (visited.Add(next)) stack.Push(next);
                    }
                }
            }

            return components;
        }
    }
}
