using Repo_Into_Graph_DataAccess.Models.Analysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_Application.Services.Analysis;
using Repo_Into_Graph_Application.Dtos.Analysis;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/analysis-runs")]
    public class AnalysisRunsController : ControllerBase
    {
        private readonly IAnalysisRunService _analysisRunService;

        public AnalysisRunsController(IAnalysisRunService analysisRunService)
        {
            _analysisRunService = analysisRunService
                ?? throw new ArgumentNullException(nameof(analysisRunService));
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<AnalysisRunDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? repoOwner = null,
            [FromQuery] string? repoName = null,
            [FromQuery] string? repoLanguage = null,
            [FromQuery] bool? isPublic = null)
        {
            var result = await _analysisRunService.GetPagedAsync(
                page, pageSize, repoOwner, repoName, repoLanguage, isPublic);

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AnalysisRunDto>> GetById(Guid id)
        {
            var dto = await _analysisRunService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AnalysisRunDto>> Create([FromBody] CreateAnalysisRunRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = await _analysisRunService.CreateAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AnalysisRunDto>> Update(
            Guid id,
            [FromBody] UpdateAnalysisRunRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = await _analysisRunService.UpdateAsync(id, request);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            return Ok(dto);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Private helper
        // ─────────────────────────────────────────────────────────────────────────
        private static AnalysisRunDto ToDto(AnalysisRun x) => new()
        {
            Id = x.Id,
            RepositoryPath = x.RepositoryPath,
            CreatedAt = x.CreatedAt,
            RepoName = x.RepoName,
            RepoOwner = x.RepoOwner,
            RepoDescription = x.RepoDescription,
            RepoUrl = x.RepoUrl,
            RepoLanguage = x.RepoLanguage,
            RepoStars = x.RepoStars,
            IsPublic = x.IsPublic,
            RepoUpdatedAt = x.RepoUpdatedAt
        };
    }
}



