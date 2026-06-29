using Repo_Into_Graph_Application.Enums;

namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    public class GenerateQuestionsRequest
    {
        public Guid BusinessId { get; set; }
        public int NumberOfQuestions { get; set; } = 5;
        public DifficultyLevel? Difficulty { get; set; }
        public string? Description { get; set; }
        public List<Guid>? FewShotExampleIds { get; set; }
    }
}





