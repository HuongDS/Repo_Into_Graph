namespace Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter
{
    public class RouterRequestDto
    {
        public string SourceCode { get; set; } = string.Empty;
        
        /// <summary>
        /// e.g. "csharp", "java", "python"
        /// </summary>
        public string Language { get; set; } = "csharp";
    }
}
