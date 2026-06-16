using System;
using System.Collections.Generic;

namespace Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate
{
    public class GenerateQuestionsFromFlowRequest
    {
        public Guid BusinessFlowId { get; set; }
        public int NumberOfQuestions { get; set; } = 5;
        public string Difficulty { get; set; } = "Medium";
        public string? AdditionalContext { get; set; }

        /// <summary>
        /// Danh sách ID các câu hỏi mẫu few-shot để AI noi theo.
        /// Nếu để trống, hệ thống sẽ tự động lấy các ví dụ cùng mức Difficulty (tối đa 5).
        /// </summary>
        public List<Guid>? FewShotExampleIds { get; set; }
    }
}
