namespace Repo_Into_Graph_Application.Services.LLMOrchestrator
{
    public interface IPromptBuilderService
    {
        string BuildSystemPrompt(int numberOfQuestions = 5, string difficulty = "Medium");
        string BuildFinalPayload(string workflowOverview, string aggregatedNodesContext);
    }
}
