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

            // --- CẠNH (Edges): dùng CALL GRAPH THẬT (CallGraphEdge), không đoán bừa ---
            //
            // TRƯỚC ĐÂY: workflowEdges được dựng bằng cách lấy list method map vào
            // Feature rồi nối "node[i] -> node[i+1]" theo ĐÚNG THỨ TỰ MÀ DB TRẢ VỀ.
            // FeatureMethodMapping không có cột thứ tự nào (chỉ có Id/FeatureId/
            // MethodSourceId/MappedAt) và GetMappingsWithMethodSourceByFeatureIdsAsync
            // không có ORDER BY, nên thứ tự đó KHÔNG liên quan gì tới việc ai gọi ai.
            // Hậu quả: đồ thị Macro (View Graph, Tầng 1) vẽ ra hoàn toàn ngược/lộn xộn
            // so với luồng gọi thật (leaf method như Repository.AddAsync lại đứng đầu,
            // Controller — entry point thật — lại nằm giữa chuỗi), và các chỉ số
            // Accuracy/Difficulty (McCabe V(G) = E - N + 2P) vốn tính trên chính các
            // cạnh này cũng bị sai theo.
            //
            // NAY: lấy cạnh Caller->Callee THẬT từ bảng CallGraphEdge (được trích xuất
            // đúng lúc parse AST ở Tầng 1), chỉ giữ lại cạnh mà CẢ Caller và Callee đều
            // là 1 trong các method thuộc Business này.
            var workflowEdges = new List<EdgeDto>();

            int globalNodeCount = 0;
            if (workflowMethods.Count > 0)
            {
                var runId = workflowMethods.First().AnalysisRunId;

                var nodeIdByMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in workflowMethods)
                {
                    var key = $"{m.ClassName?.Trim()}|{m.MethodName?.Trim()}";
                    // Nếu trùng key (hiếm, ví dụ overload cùng tên) thì giữ method đầu tiên
                    // gặp — vẫn tốt hơn hẳn so với không có cạnh nào.
                    nodeIdByMethod.TryAdd(key, m.Id.ToString());
                }

                var callGraphEdges = await _unitOfWork.CallGraphEdges.GetByAnalysisRunIdAsync(runId);
                foreach (var e in callGraphEdges)
                {
                    var callerKey = $"{e.CallerClass?.Trim()}|{e.CallerMethod?.Trim()}";
                    var calleeKey = $"{e.CalleeClass?.Trim()}|{e.CalleeMethod?.Trim()}";

                    if (nodeIdByMethod.TryGetValue(callerKey, out var fromId) &&
                        nodeIdByMethod.TryGetValue(calleeKey, out var toId) &&
                        fromId != toId)
                    {
                        workflowEdges.Add(new EdgeDto
                        {
                            FromNodeId = fromId,
                            ToNodeId = toId,
                            Label = e.ConditionContext
                        });
                    }
                }

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
