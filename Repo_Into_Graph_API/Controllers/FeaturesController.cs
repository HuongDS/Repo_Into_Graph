using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.Features;
using Repo_Into_Graph_Application.Dtos.Feature;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/features")]
    public class FeaturesController : ControllerBase
    {
        private readonly IFeatureService _featureService;

        public FeaturesController(IFeatureService featureService)
        {
            _featureService = featureService
                ?? throw new ArgumentNullException(nameof(featureService));
        }

        [HttpGet]
        public async Task<ActionResult<FeaturePagedResult>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? analysisRunId = null,
            [FromQuery] string? name = null)
        {
            var result = await _featureService.GetPagedAsync(
                page, pageSize, analysisRunId, name);

            return Ok(result);
        }

        [HttpGet("by-analysis-run/{analysisRunId:guid}")]
        public async Task<ActionResult<System.Collections.Generic.IEnumerable<FeatureDetailDto>>> GetByAnalysisRun(Guid analysisRunId)
        {
            var features = await _featureService.GetAllByAnalysisRunAsync(analysisRunId);
            return Ok(features);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<FeatureDetailDto>> GetById(Guid id)
        {
            var dto = await _featureService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy Feature với ID: {id}" });

            return Ok(dto);
        }
    }
}
