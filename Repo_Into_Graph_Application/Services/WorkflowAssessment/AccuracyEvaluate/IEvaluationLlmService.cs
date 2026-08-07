using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate
{
    public interface IEvaluationLlmService
    {
        Task<string> EvaluateWithLlmAsync(string systemPrompt, string userPrompt);
    }
}
