using Repo_Into_Graph.Repo_Into_Graph.Dtos;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Services
{
    public interface IAnalysisService
    {
        Task<AnalysisResponseDto> AnalyzeRepositoryAsync(string repositoryPath, string? outputDir);
    }
}
