using Repo_Into_Graph_Application.Dtos.QuestionEvalution;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;
using Repo_Into_Graph_DataAccess.Models.FewShot;

namespace Repo_Into_Graph_Application.Services.AI
{
    public interface IAIService
    {
        /// <summary>
        /// Sinh câu hỏi vấn đáp dựa trên một Feature (gồm Source Code và Business Flow Context).
        /// Cung cấp một cái nhìn toàn diện (Unified) cho mô hình AI để ra câu hỏi chất lượng nhất.
        /// </summary>
        Task<IEnumerable<GeneratedQuestionDto>> GenerateUnifiedQuestionsAsync(
            FeatureModel feature,
            string codeBuilder,
            int numberOfQuestions,
            string difficulty,
            string? additionalContext = null,
            IEnumerable<FewShotExample>? fewShotExamples = null);

        Task<IEnumerable<QuestionEvaluationResultDto>> EvaluateQuestionsAsync(
            string dataFlowMermaidGraph,
            string codeBuilder, 
            IEnumerable<GeneratedQuestionDto> generatedQuestions);



    }
}





