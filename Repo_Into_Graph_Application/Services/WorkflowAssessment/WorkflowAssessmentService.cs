using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.CoverageEvaluate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.DifficultyEvaluate;
using Repo_Into_Graph_DataAccess.Repository.Interface;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    public class WorkflowAssessmentService : IWorkflowAssessmentService
    {
        private readonly ILogger<WorkflowAssessmentService> _logger;
        private readonly IBusinessRepository _businessRepository;
        private readonly IFeatureBusinessMappingRepository _featureBusinessMappingRepository;
        private readonly IFeatureMethodMappingRepository _featureMethodMappingRepository;
        private readonly IMethodSourceRepository _methodSourceRepository;
        private readonly IAnalysisRunRepository _analysisRunRepository;

        public ICoverageAssessmentService Coverage { get; }
        public IAccuracyAssessmentService Accuracy { get; }
        public IDifficultyAssessmentService Difficulty { get; }

        public WorkflowAssessmentService(
            ILogger<WorkflowAssessmentService> logger,
            IBusinessRepository businessRepository,
            IFeatureBusinessMappingRepository featureBusinessMappingRepository,
            IFeatureMethodMappingRepository featureMethodMappingRepository,
            IMethodSourceRepository methodSourceRepository,
            IAnalysisRunRepository analysisRunRepository,
            ICoverageAssessmentService coverageAssessmentService,
            IAccuracyAssessmentService accuracyAssessmentService,
            IDifficultyAssessmentService difficultyAssessmentService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
            _featureBusinessMappingRepository = featureBusinessMappingRepository ?? throw new ArgumentNullException(nameof(featureBusinessMappingRepository));
            _featureMethodMappingRepository = featureMethodMappingRepository ?? throw new ArgumentNullException(nameof(featureMethodMappingRepository));
            _methodSourceRepository = methodSourceRepository ?? throw new ArgumentNullException(nameof(methodSourceRepository));
            _analysisRunRepository = analysisRunRepository ?? throw new ArgumentNullException(nameof(analysisRunRepository));
            
            Coverage = coverageAssessmentService ?? throw new ArgumentNullException(nameof(coverageAssessmentService));
            Accuracy = accuracyAssessmentService ?? throw new ArgumentNullException(nameof(accuracyAssessmentService));
            Difficulty = difficultyAssessmentService ?? throw new ArgumentNullException(nameof(difficultyAssessmentService));
        }

        public async Task<BusinessWorkflowGraphDto> GetBusinessWorkflowGraphAsync(Guid businessId)
        {
            var (workflowGraph, globalGraph) = await BuildGraphsFromDbAsync(businessId);

            return new BusinessWorkflowGraphDto
            {
                BusinessId = businessId,
                BusinessName = workflowGraph.WorkflowName,
                WorkflowNodeCount = workflowGraph.Nodes.Count,
                GlobalNodeCount = globalGraph.TotalNodeCount,
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

        public async Task<(WorkflowGraphDto WorkflowGraph, GlobalGraphDto GlobalGraph)> BuildGraphsFromDbAsync(Guid businessId)
        {
            var featureIds = await _featureBusinessMappingRepository.GetFeatureIdsByBusinessIdAsync(businessId);

            var featureMappings = featureIds.Count > 0
                ? await _featureMethodMappingRepository.GetMappingsWithMethodSourceByFeatureIdsAsync(featureIds)
                : new List<Repo_Into_Graph_DataAccess.Models.Feature.FeatureMethodMapping>();

            var workflowMethods = featureIds.Count > 0
                ? await _featureMethodMappingRepository.GetMethodSourcesByFeatureIdsAsync(featureIds)
                : new List<Repo_Into_Graph_DataAccess.Models.Method.MethodSourceRecord>();

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
                    Description = $"{m.ClassName} {m.MethodName} {string.Join(" ", kws)} {ExtractKeywordsFromSource(m.SourceCode)}",
                    Keywords = kws,
                    SourceCode = m.SourceCode ?? string.Empty
                };
            }).ToList();

            var workflowEdges = new List<EdgeDto>();
            var methodsByFeature = featureMappings
                .Where(m => m.MethodSource != null)
                .GroupBy(m => m.FeatureId)
                .ToList();

            foreach (var featureGroup in methodsByFeature)
            {
                var methods = featureGroup.Select(m => m.MethodSourceId).Distinct().ToList();
                for (int i = 0; i < methods.Count - 1; i++)
                {
                    workflowEdges.Add(new EdgeDto
                    {
                        FromNodeId = methods[i].ToString(),
                        ToNodeId = methods[i + 1].ToString()
                    });
                }
            }

            int globalNodeCount = 0;
            if (workflowMethods.Count > 0)
            {
                var runId = workflowMethods.First().AnalysisRunId;
                var analysisRun = await _analysisRunRepository.GetByIdAsync(runId);
                if (analysisRun != null)
                {
                    globalNodeCount = analysisRun.GlobalNodeCount;
                }
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
                AllNodes = new List<NodeDto>(),
                AllEdges = new List<EdgeDto>(),
                TotalNodeCount = globalNodeCount
            };

            return (workflowGraph, globalGraph);
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
