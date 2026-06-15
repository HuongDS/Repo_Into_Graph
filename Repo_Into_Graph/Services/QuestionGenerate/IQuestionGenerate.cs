using System.Collections.Generic;
using System.Threading.Tasks;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.QuestionGenerate
{
    public interface IQuestionGenerate
    {
        Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestionsAsync(GenerateQuestionsRequest request);
    }
}
