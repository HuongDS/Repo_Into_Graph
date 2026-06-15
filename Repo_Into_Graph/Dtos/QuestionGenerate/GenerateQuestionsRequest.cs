namespace Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate
{
    public class GenerateQuestionsRequest
    {
        public Guid FeatureId { get; set; }
        public int NumberOfQuestions { get; set; }
        public string Difficulty { get; set; } = "Medium";
        public string? Description { get; set; }
    }
}
