using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    /// <summary>
    /// Kết quả trả về khi sinh câu hỏi từ một Business Flow.
    /// </summary>
    public class GenerateQuestionsFromFlowResponse
    {
        public Guid BusinessFlowId { get; set; }
        public string BusinessFlowName { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public int TotalSteps { get; set; }
        public int FewShotUsed { get; set; }
        public IEnumerable<GeneratedQuestionDto> Questions { get; set; }
            = Enumerable.Empty<GeneratedQuestionDto>();
    }
}





