using Repo_Into_Graph_Application.Dtos.Feature;
using Repo_Into_Graph_Application.Dtos.Analysis;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Analysis
{
    public interface IAnalysisService
    {
        Task<AnalysisResponseDto> AnalyzeRepositoryAsync(string repositoryPath, string? outputDir);
    }
}





