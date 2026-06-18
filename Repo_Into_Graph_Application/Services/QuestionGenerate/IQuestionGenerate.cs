using System.Collections.Generic;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;

namespace Repo_Into_Graph_Application.Services.QuestionGenerate
{
    public interface IQuestionGenerate
    {
        Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestionsAsync(GenerateQuestionsRequest request);

        /// <summary>
        /// Sinh câu hỏi từ một Business Flow cụ thể (kèm few-shot examples).
        /// Logic đã được chuyển từ controller vào service.
        /// </summary>
        Task<GenerateQuestionsFromFlowResponse> GenerateQuestionsFromFlowAsync(GenerateQuestionsFromFlowRequest request);
    }
}





