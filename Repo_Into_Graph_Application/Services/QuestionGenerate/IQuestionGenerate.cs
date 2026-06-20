using System.Collections.Generic;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.QuestionEvalution;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;

namespace Repo_Into_Graph_Application.Services.QuestionGenerate
{
    public interface IQuestionGenerate
    {
        Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request);

        Task<IEnumerable<QuestionEvaluationResultDto>> EvaluateQuestionsAsync(string dataFlowMermaidGraph, string codeBuilder, IEnumerable<GeneratedQuestionDto> generatedQuestions);
    }
}





