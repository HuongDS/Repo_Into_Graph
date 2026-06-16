using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Services.Analysis;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    /// <summary>
    /// CRUD + phân trang cho bảng analysis_runs.
    /// Base route: /api/analysis-runs
    /// </summary>
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

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/analysis-runs?page=1&pageSize=10&repoOwner=...&repoName=...
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Lấy danh sách analysis runs có phân trang và lọc theo thông tin repository.
        /// </summary>
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

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/analysis-runs/{id}
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Lấy chi tiết một analysis run theo ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AnalysisRunDto>> GetById(Guid id)
        {
            var dto = await _analysisRunService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            return Ok(dto);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST /api/analysis-runs
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Tạo mới một analysis run (kèm thông tin repository).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AnalysisRunDto>> Create([FromBody] CreateAnalysisRunRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = await _analysisRunService.CreateAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PUT /api/analysis-runs/{id}
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Cập nhật thông tin repository metadata của một analysis run.
        /// </summary>
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
        // DELETE /api/analysis-runs/{id}
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Xóa một analysis run (cascade xóa các bảng liên quan).
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _analysisRunService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            return NoContent();
        }
    }
}
