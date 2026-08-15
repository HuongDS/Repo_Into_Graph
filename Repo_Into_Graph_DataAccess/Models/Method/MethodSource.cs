namespace Repo_Into_Graph_DataAccess.Models.Method;

public class MethodSource
{
    public required string ClassName { get; set; }
    public required string MethodName { get; set; }
    public required string SourceCode { get; set; }
    /// <summary>Language/framework the source was extracted from.</summary>
    public string? Language { get; set; }
    public Consts.NodeType Type { get; set; } = Consts.NodeType.Activity;
}




