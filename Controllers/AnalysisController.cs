using Repo_Into_Graph_Application.Services.Analysis;
using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Dtos.Analysis;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly IAnalysisService _analysisService;

        public AnalysisController(IAnalysisService analysisService)
        {
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        }

        [HttpPost("analyze")]
        public async Task<ActionResult<AnalysisResponseDto>> Analyze([FromBody] AnalyzeRequest request)
        {
            var response = await _analysisService.AnalyzeRepositoryAsync(request.RepositoryPath, request.OutputDir);
            return Ok(response);
        }
    }

    public record AnalyzeRequest(string RepositoryPath, string? OutputDir);
}




