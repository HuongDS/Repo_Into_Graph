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
using AutoMapper;
using Repo_Into_Graph_Application.Helper;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    public class WorkflowAssessmentService : IWorkflowAssessmentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ICoverageAssessmentService Coverage { get; }
        public IAccuracyAssessmentService Accuracy { get; }
        public IDifficultyAssessmentService Difficulty { get; }

        private readonly IMapper _mapper;

        public WorkflowAssessmentService(
            IUnitOfWork unitOfWork,
            ICoverageAssessmentService coverageAssessmentService,
            IAccuracyAssessmentService accuracyAssessmentService,
            IDifficultyAssessmentService difficultyAssessmentService,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            Coverage = coverageAssessmentService ?? throw new ArgumentNullException(nameof(coverageAssessmentService));
            Accuracy = accuracyAssessmentService ?? throw new ArgumentNullException(nameof(accuracyAssessmentService));
            Difficulty = difficultyAssessmentService ?? throw new ArgumentNullException(nameof(difficultyAssessmentService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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
                Nodes = workflowGraph.Nodes.Select(n => _mapper.Map<BusinessWorkflowNodeDto>(n)).ToList(),
                Edges = workflowGraph.Edges.Select(e => _mapper.Map<BusinessWorkflowEdgeDto>(e)).ToList()
            };
        }

        public async Task<WorkflowDataDto> GetWorkflowDataAsync(Guid businessId)
        {
            var (workflowGraph, _) = await BuildGraphsFromDbAsync(businessId);

            return new WorkflowDataDto
            {
                WorkflowName = workflowGraph.WorkflowName,
                Nodes = workflowGraph.Nodes.Select(n => _mapper.Map<WorkflowNodeInputDto>(n)).ToList(),
                Edges = workflowGraph.Edges.Select(e => _mapper.Map<WorkflowEdgeInputDto>(e)).ToList()
            };
        }

        public async Task<(WorkflowGraphDto WorkflowGraph, GlobalGraphDto GlobalGraph)> BuildGraphsFromDbAsync(Guid businessId)
        {
            var featureIds = await _unitOfWork.FeatureBusinessMappings.GetFeatureIdsByBusinessIdAsync(businessId);

            var featureMappings = featureIds.Count > 0
                ? await _unitOfWork.FeatureMethodMappings.GetMappingsWithMethodSourceByFeatureIdsAsync(featureIds)
                : new List<Repo_Into_Graph_DataAccess.Models.Feature.FeatureMethodMapping>();

            var workflowMethods = featureIds.Count > 0
                ? await _unitOfWork.FeatureMethodMappings.GetMethodSourcesByFeatureIdsAsync(featureIds)
                : new List<Repo_Into_Graph_DataAccess.Models.Method.MethodSourceRecord>();

            var workflowNodes = workflowMethods.Select(m =>
            {
                var kws = new List<string> { m.ClassName.Trim(), m.MethodName.Trim() };
                kws.AddRange(SplitCamelCase.Split(m.MethodName));
                kws.AddRange(SplitCamelCase.Split(m.ClassName));
                return new NodeDto
                {
                    Id = m.Id.ToString(),
                    Name = $"{m.ClassName}.{m.MethodName}",
                    Type = m.Type == Repo_Into_Graph_DataAccess.Consts.NodeType.DecisionGateway
                        ? NodeType.DecisionGateway
                        : NodeType.Activity,
                    Description = $"{m.ClassName} {m.MethodName} {string.Join(" ", kws)} {ExtractKeywordsFromSource.Extract(m.SourceCode)}",
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
                var analysisRun = await _unitOfWork.AnalysisRuns.GetByIdAsync(runId);
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
    }
}
