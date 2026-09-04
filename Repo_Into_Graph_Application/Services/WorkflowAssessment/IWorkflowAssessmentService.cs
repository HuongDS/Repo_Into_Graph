using System;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.CoverageEvaluate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.DifficultyEvaluate;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    /// <summary>
    /// Facade Orchestrator cho hệ thống WorkflowAssessment.
    /// </summary>
    public interface IWorkflowAssessmentService
    {
        ICoverageAssessmentService Coverage { get; }
        IAccuracyAssessmentService Accuracy { get; }
        IDifficultyAssessmentService Difficulty { get; }

        /// <summary>
        /// Lấy cấu trúc đồ thị (Nodes và Edges) của một Business Flow dưới dạng BusinessWorkflowGraphDto.
        /// </summary>
        Task<BusinessWorkflowGraphDto> GetBusinessWorkflowGraphAsync(Guid businessId);

        /// <summary>
        /// Lấy cấu trúc đồ thị luồng nghiệp vụ (WorkflowDataDto) từ CSDL dựa trên BusinessId.
        /// </summary>
        Task<WorkflowDataDto> GetWorkflowDataAsync(Guid businessId);

        Task<(WorkflowGraphDto WorkflowGraph, GlobalGraphDto GlobalGraph)> BuildGraphsFromDbAsync(Guid businessId);
    }
}
