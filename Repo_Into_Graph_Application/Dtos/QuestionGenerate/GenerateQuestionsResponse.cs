using System;
using System.Collections.Generic;
using System.Linq;

namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    /// <summary>
    /// Kết quả trả về khi sinh câu hỏi từ một Business Flow.
    /// </summary>
    public class GenerateQuestionsResponse
    {
        public Guid FeatureId { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public int TotalSteps { get; set; }
        public int FewShotUsed { get; set; }
        public IEnumerable<Repo_Into_Graph_Application.Dtos.QuestionEvalution.QuestionEvaluationResultDto> EvaluatedQuestions { get; set; } = new List<Repo_Into_Graph_Application.Dtos.QuestionEvalution.QuestionEvaluationResultDto>();
    }
}

