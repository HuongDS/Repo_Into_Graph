using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Repo_Into_Graph_Application.Dtos.LLMOrchestrator
{
    public class CodeQuestion
    {
        [JsonPropertyName("questionId")]
        public string QuestionId { get; set; } = string.Empty;

        [JsonPropertyName("questionText")]
        public string QuestionText { get; set; } = string.Empty;

        [JsonPropertyName("questionType")]
        public string QuestionType { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public List<string> Options { get; set; } = new List<string>();

        [JsonPropertyName("correctAnswer")]
        public string CorrectAnswer { get; set; } = string.Empty;

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;

        [JsonPropertyName("targetNode")]
        public string TargetNode { get; set; } = string.Empty;

        [JsonPropertyName("difficultyLevel")]
        public string DifficultyLevel { get; set; } = string.Empty;
    }
}
