using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_Application.Dtos.WorkflowAssessment
{
    /// <summary>
    /// DTO chứa cấu trúc đồ thị (Nodes và Edges) của một Business Flow.
    /// Trả về cho API GET /api/businesses/{businessId}/graph.
    /// </summary>
    public class BusinessWorkflowGraphDto
    {
        public Guid BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public int WorkflowNodeCount { get; set; }
        public int GlobalNodeCount { get; set; }
        public List<BusinessWorkflowNodeDto> Nodes { get; set; } = new();
        public List<BusinessWorkflowEdgeDto> Edges { get; set; } = new();
    }

    /// <summary>
    /// Thông tin chi tiết một Nút trong đồ thị luồng nghiệp vụ của Business.
    /// </summary>
    public class BusinessWorkflowNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // StartEvent / Activity / DecisionGateway / EndEvent / MergeGateway
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thông tin liên kết (cạnh) giữa các nút trong đồ thị luồng nghiệp vụ của Business.
    /// </summary>
    public class BusinessWorkflowEdgeDto
    {
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string? Condition { get; set; }
    }
}
