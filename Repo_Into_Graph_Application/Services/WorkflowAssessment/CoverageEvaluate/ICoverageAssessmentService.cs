using System;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.CoverageEvaluate
{
    public interface ICoverageAssessmentService
    {
        Task<AssessmentResultDto> AssessAsync(AssessmentRequestDto request);

        Task<CoverageAssessmentResultDto> AssessCoverageBatchAsync(
            GenerateQuestionsResponse response, 
            WorkflowGraphDto workflowGraph, 
            GlobalGraphDto globalGraph);
    }
}
