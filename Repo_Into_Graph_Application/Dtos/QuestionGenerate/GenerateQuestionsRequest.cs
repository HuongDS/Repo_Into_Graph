namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    public class GenerateQuestionsRequest
    {
        public Guid FeatureId { get; set; }
        public int NumberOfQuestions { get; set; } = 5;
        public string Difficulty { get; set; } = "Medium";
        public string? Description { get; set; }
        public List<Guid>? FewShotExampleIds { get; set; }
    }
}





