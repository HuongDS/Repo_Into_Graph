namespace Repo_Into_Graph.Models;

public class DataFlowNode
{
    public required string VariableName { get; set; }
    public required string DataType { get; set; }
    public string? SourceLocation { get; set; }
    public required List<string> PassedThroughMethods { get; set; } = new();
    public string? SinkLocation { get; set; }
    public string? SinkType { get; set; }
}
