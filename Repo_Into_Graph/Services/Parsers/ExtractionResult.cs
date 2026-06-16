using global::Repo_Into_Graph.Models;
using global::Repo_Into_Graph.Repo_Into_Graph.Models.Method;

namespace Repo_Into_Graph.Services.Parsers;

/// <summary>
/// Result returned by any ILanguageParser implementation.
/// Contains extracted call graph edges and method source code records.
/// </summary>
public class ExtractionResult
{
    public List<CallGraphEdge> CallGraphEdges { get; set; } = new();
    public List<MethodSource> MethodSources { get; set; } = new();
}
