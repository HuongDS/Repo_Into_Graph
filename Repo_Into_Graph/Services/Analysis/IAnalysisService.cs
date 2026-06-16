using Repo_Into_Graph.Repo_Into_Graph.Dtos.Feature;
using global::Repo_Into_Graph.Repo_Into_Graph.Dtos.Analysis;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.Analysis
{
    public interface IAnalysisService
    {
        Task<AnalysisResponseDto> AnalyzeRepositoryAsync(string repositoryPath, string? outputDir);
    }
}
