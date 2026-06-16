using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.AI
{
    public interface IAIService
    {
        Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestions(int numberOfQuestions, GenerateQuestionsRequest request, string codeBuilder, string contextBuilder);
    }
}
