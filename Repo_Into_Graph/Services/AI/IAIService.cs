using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;
using Repo_Into_Graph.Repo_Into_Graph.Models.FewShot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.AI
{
    public interface IAIService
    {
        Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestions(int numberOfQuestions, GenerateQuestionsRequest request, string codeBuilder, string contextBuilder);

        /// <summary>
        /// Sinh câu hỏi vấn đáp dựa trên một Business Flow cụ thể, bao gồm entry point, chuỗi bước gọi, và Mermaid diagram.
        /// Có thể truyền thêm danh sách few-shot examples của giảng viên để AI noi theo.
        /// </summary>
        Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestionsFromBusinessFlowAsync(
            BusinessFlow businessFlow,
            int numberOfQuestions,
            string difficulty,
            string? additionalContext = null,
            IEnumerable<FewShotExample>? fewShotExamples = null);
    }
}
