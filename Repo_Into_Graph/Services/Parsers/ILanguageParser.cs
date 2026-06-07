using Repo_Into_Graph.Models;

namespace Repo_Into_Graph.Services.Parsers;

/// <summary>
/// Interface for language-specific code parsers.
/// Each implementation is responsible for extracting call graph edges
/// and method sources from source code of a specific programming language.
/// </summary>
public interface ILanguageParser
{
    /// <summary>
    /// The display name of the language/framework this parser handles.
    /// </summary>
    string LanguageName { get; }

    /// <summary>
    /// File extensions this parser handles (e.g. ".java", ".py").
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Parse source code from a single file and extract call graph edges and method sources.
    /// </summary>
    /// <param name="filePath">Absolute path to the source file.</param>
    /// <param name="sourceCode">Raw source code content of the file.</param>
    /// <returns>ExtractionResult containing call graph edges and method sources.</returns>
    Task<ExtractionResult> ParseAsync(string filePath, string sourceCode);
}
