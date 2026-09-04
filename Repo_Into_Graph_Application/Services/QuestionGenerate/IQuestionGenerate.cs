using System.Collections.Generic;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;

namespace Repo_Into_Graph_Application.Services.QuestionGenerate
{
    public interface IQuestionGenerate
    {
        Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request);

    }
}





