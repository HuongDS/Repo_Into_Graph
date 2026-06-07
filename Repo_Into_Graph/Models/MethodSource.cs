namespace Repo_Into_Graph.Models;

public class MethodSource
{
    public required string ClassName { get; set; }
    public required string MethodName { get; set; }
    public required string SourceCode { get; set; }
    /// <summary>Language/framework the source was extracted from.</summary>
    public string? Language { get; set; }
}
