using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    public interface ISemanticMappingHelper
    {
        Task<List<ExtractedPathStepDto>> GetSemanticMappingAsync(Guid businessId, string question, List<WorkflowNodeInputDto> nodes, double[][]? precomputedNodeVectors = null);
    }
}
