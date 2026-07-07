namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    public class GeneratedQuestionDto
    {
        public string Question { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;

        public string[] TargetedEntryPoints { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Độ bao phủ code riêng của câu hỏi này (tỉ lệ method mà câu hỏi chạm tới / tổng số method của Business).
        /// </summary>
        public double Coverage { get; set; } = 0.0;
    }
}





