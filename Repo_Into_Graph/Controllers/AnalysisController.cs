using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph.Services;
using Repo_Into_Graph.Repo_Into_Graph.Dtos;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
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
            try
            {
                var response = await _analysisService.AnalyzeRepositoryAsync(request.RepositoryPath, request.OutputDir);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (System.IO.DirectoryNotFoundException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"System error: {ex.Message}" });
            }
        }
    }

    public record AnalyzeRequest(string RepositoryPath, string? OutputDir);
}
