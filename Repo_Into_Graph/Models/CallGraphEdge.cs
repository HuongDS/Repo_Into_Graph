namespace Repo_Into_Graph.Models;

public class CallGraphEdge
{
    public required string CallerClass { get; set; }
    public required string CallerMethod { get; set; }
    public required string CalleeClass { get; set; }
    public required string CalleeMethod { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}
