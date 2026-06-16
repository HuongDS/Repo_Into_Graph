using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using System;
using System.Linq;
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
        private readonly IUnitOfWork _unitOfWork;

        public AnalysisRunsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/analysis-runs?page=1&pageSize=10&repoOwner=...&repoLanguage=...
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
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            // Lấy IQueryable để áp dụng filter + pagination hiệu quả
            IQueryable<AnalysisRun> query = _unitOfWork.AnalysisRuns
                .AsQueryable()
                .OrderByDescending(x => x.CreatedAt);

            // Áp dụng bộ lọc
            if (!string.IsNullOrWhiteSpace(repoOwner))
                query = query.Where(x => x.RepoOwner != null &&
                                         x.RepoOwner.ToLower().Contains(repoOwner.ToLower()));

            if (!string.IsNullOrWhiteSpace(repoName))
                query = query.Where(x => x.RepoName != null &&
                                         x.RepoName.ToLower().Contains(repoName.ToLower()));

            if (!string.IsNullOrWhiteSpace(repoLanguage))
                query = query.Where(x => x.RepoLanguage != null &&
                                         x.RepoLanguage.ToLower().Contains(repoLanguage.ToLower()));

            if (isPublic.HasValue)
                query = query.Where(x => x.IsPublic == isPublic.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => ToDto(x))
                .ToListAsync();

            return Ok(new PagedResult<AnalysisRunDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
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
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            return Ok(ToDto(entity));
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

            var entity = new AnalysisRun
            {
                Id             = Guid.NewGuid(),
                RepositoryPath = request.RepositoryPath.Trim(),
                CreatedAt      = DateTime.UtcNow,
                RepoName       = request.RepoName?.Trim(),
                RepoOwner      = request.RepoOwner?.Trim(),
                RepoDescription = request.RepoDescription?.Trim(),
                RepoUrl        = request.RepoUrl?.Trim(),
                RepoLanguage   = request.RepoLanguage?.Trim(),
                RepoStars      = request.RepoStars,
                IsPublic       = request.IsPublic,
                RepoUpdatedAt  = request.RepoUpdatedAt
            };

            await _unitOfWork.AnalysisRuns.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PUT /api/analysis-runs/{id}
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Cập nhật thông tin repository metadata của một analysis run.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AnalysisRunDto>> Update(Guid id, [FromBody] UpdateAnalysisRunRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            // Chỉ ghi đè khi giá trị được truyền lên (khác null)
            if (request.RepoName is not null)
                entity.RepoName = request.RepoName.Trim();
            if (request.RepoOwner is not null)
                entity.RepoOwner = request.RepoOwner.Trim();
            if (request.RepoDescription is not null)
                entity.RepoDescription = request.RepoDescription.Trim();
            if (request.RepoUrl is not null)
                entity.RepoUrl = request.RepoUrl.Trim();
            if (request.RepoLanguage is not null)
                entity.RepoLanguage = request.RepoLanguage.Trim();
            if (request.RepoStars is not null)
                entity.RepoStars = request.RepoStars;
            if (request.IsPublic is not null)
                entity.IsPublic = request.IsPublic;
            if (request.RepoUpdatedAt is not null)
                entity.RepoUpdatedAt = request.RepoUpdatedAt;

            _unitOfWork.AnalysisRuns.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            return Ok(ToDto(entity));
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
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null)
                return NotFound(new { error = $"Không tìm thấy AnalysisRun với ID: {id}" });

            _unitOfWork.AnalysisRuns.Delete(entity);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Private helper
        // ─────────────────────────────────────────────────────────────────────────
        private static AnalysisRunDto ToDto(AnalysisRun x) => new()
        {
            Id              = x.Id,
            RepositoryPath  = x.RepositoryPath,
            CreatedAt       = x.CreatedAt,
            RepoName        = x.RepoName,
            RepoOwner       = x.RepoOwner,
            RepoDescription = x.RepoDescription,
            RepoUrl         = x.RepoUrl,
            RepoLanguage    = x.RepoLanguage,
            RepoStars       = x.RepoStars,
            IsPublic        = x.IsPublic,
            RepoUpdatedAt   = x.RepoUpdatedAt
        };
    }
}
