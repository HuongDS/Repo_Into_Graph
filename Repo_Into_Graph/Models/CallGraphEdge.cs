namespace Repo_Into_Graph.Models;

public class CallGraphEdge
{
    public required string CallerClass { get; set; }
    public required string CallerMethod { get; set; }
    public required string CalleeClass { get; set; }
    public required string CalleeMethod { get; set; }
    /// <summary>Language/framework the edge was extracted from (e.g. "C# (.NET)", "Java (Spring Boot)").</summary>
    public string? Language { get; set; }
}
