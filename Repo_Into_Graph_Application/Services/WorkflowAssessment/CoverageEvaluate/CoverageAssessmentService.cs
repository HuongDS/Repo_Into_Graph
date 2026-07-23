using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.CoverageEvaluate
{
    public class CoverageAssessmentService : ICoverageAssessmentService
    {
        private readonly ILogger<CoverageAssessmentService> _logger;
        private readonly ISemanticMappingHelper _semanticMappingHelper;
        private readonly IDistributedCache _cache;

        public CoverageAssessmentService(
            ILogger<CoverageAssessmentService> logger,
            ISemanticMappingHelper semanticMappingHelper,
            IDistributedCache cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _semanticMappingHelper = semanticMappingHelper ?? throw new ArgumentNullException(nameof(semanticMappingHelper));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<AssessmentResultDto> AssessAsync(AssessmentRequestDto request)
        {
            var result = new AssessmentResultDto();

            var inputNodes = request.SelectedWorkflow.Nodes.Select(n => new WorkflowNodeInputDto
            {
                NodeId = n.Id,
                NodeName = n.Name,
                Description = n.Description,
                SourceCode = n.SourceCode
            }).ToList();

            // Bước 1: Semantic Mapping
            var extractedPath = await _semanticMappingHelper.GetSemanticMappingAsync(Guid.Empty, request.Question, inputNodes, null, request.TargetedEntryPoints);
            
            var activeNodes = request.SelectedWorkflow.Nodes
                .Where(n => extractedPath.Any(p => p.NodeId == n.Id))
                .ToList();

            result.ActiveNodeIds = activeNodes.Select(n => n.Id).ToList();
            result.ActiveNodes = activeNodes;

            // Bước 2: Metrics (Coverage)
            Step2_CalculateMetrics(activeNodes, request.SelectedWorkflow.Nodes, request.GlobalGraph.TotalNodeCount, result);

            return result;
        }

        public async Task<CoverageAssessmentResultDto> AssessCoverageBatchAsync(
            GenerateQuestionsResponse response,
            WorkflowGraphDto workflowGraph,
            GlobalGraphDto globalGraph)
        {
            var questions = (response.GeneratedQuestionDtos ?? Enumerable.Empty<GeneratedQuestionDto>()).ToList();
            if (questions.Count == 0)
            {
                return new CoverageAssessmentResultDto
                {
                    BusinessId = response.BusinessId,
                    BusinessName = response.BusinessName,
                    TotalQuestions = 0
                };
            }

            // 1. Kiểm tra Cache trước
            string cacheKey = $"coverage_batch_{response.BusinessId}_{string.Join("_", questions.Select(q => q.Question.GetHashCode()))}";
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("[CoverageAssessmentService] Đã lấy kết quả từ Cache.");
                return JsonSerializer.Deserialize<CoverageAssessmentResultDto>(cachedData)!;
            }

            // 2. Chạy Pipeline cho từng câu hỏi
            var questionResults = new List<QuestionCoverageDto>(questions.Count);

            foreach (var q in questions)
            {
                if (string.IsNullOrWhiteSpace(q.Question)) continue;

                AssessmentResultDto? graphResult = null;
                try
                {
                    var request = new AssessmentRequestDto
                    {
                        Question = q.Question,
                        SelectedWorkflow = workflowGraph,
                        GlobalGraph = globalGraph,
                        TargetedEntryPoints = q.TargetedEntryPoints
                    };
                    graphResult = await AssessAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CoverageAssessmentService] Lỗi đánh giá câu hỏi: '{Q}'", q.Question);
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

            // 3. Thống kê tổng hợp
            int total = questionResults.Count;
            double avgCoverage = total > 0
                ? questionResults.Select(r => r.Coverage).DefaultIfEmpty(0.0).Average()
                : 0.0;

            var finalResult = new CoverageAssessmentResultDto
            {
                BusinessId = response.BusinessId,
                BusinessName = response.BusinessName,
                TotalQuestions = total,
                AverageTotalCoverage = avgCoverage,
                WorkflowNodeCount = workflowGraph.Nodes.Count,
                GlobalNodeCount = globalGraph.TotalNodeCount,
                QuestionResults = questionResults
            };

            // 4. Lưu vào Cache
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalResult), cacheOptions);

            return finalResult;
        }

        private void Step2_CalculateMetrics(
            List<NodeDto> activeNodes,
            List<NodeDto> workflowNodes,
            int globalCount,
            AssessmentResultDto result)
        {
            int activeCount = activeNodes?.Count ?? 0;
            int workflowCount = workflowNodes?.Count ?? 0;

            double coverageWG = globalCount > 0 ? (double)workflowCount / globalCount : 0.0;
            double coverageAW = workflowCount > 0 ? (double)activeCount / workflowCount : 0.0;

            result.CoverageWorkflowOverGlobal = coverageWG;
            result.CoverageActiveOverWorkflow = coverageAW;
            result.TotalCoverage = coverageWG * coverageAW;
        }
    }
}
