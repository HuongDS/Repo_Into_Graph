namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    public class GeneratedQuestionDto
    {
        public string Question { get; set; } = string.Empty;
        public string SuggestedAnswer { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;

        public string[] TargetedEntryPoints { get; set; } = Array.Empty<string>();

        //public double Coverage { get; set; } = 0.0;
    }
}





