using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_Application.Dtos.Analysis;
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Analysis
{
    public class AnalysisRunService : IAnalysisRunService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AnalysisRunService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        // ─── GET (paged) ──────────────────────────────────────────────────────────

        public async Task<PagedResult<AnalysisRunDto>> GetPagedAsync(
            int page,
            int pageSize,
            string? repoOwner,
            string? repoName,
            string? repoLanguage,
            bool? isPublic)
        {
            // Chuẩn hóa tham số phân trang
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            IQueryable<AnalysisRun> query = _unitOfWork.AnalysisRuns
                .AsQueryable()
                .OrderByDescending(x => x.CreatedAt);

            // Áp dụng bộ lọc (tìm kiếm không phân biệt hoa/thường)
            if (!string.IsNullOrWhiteSpace(repoOwner))
                query = query.Where(x => x.RepoOwner != null &&
                                         x.RepoOwner.ToLower().Contains(repoOwner.Trim().ToLower()));

            if (!string.IsNullOrWhiteSpace(repoName))
                query = query.Where(x => x.RepoName != null &&
                                         x.RepoName.ToLower().Contains(repoName.Trim().ToLower()));

            if (!string.IsNullOrWhiteSpace(repoLanguage))
                query = query.Where(x => x.RepoLanguage != null &&
                                         x.RepoLanguage.ToLower().Contains(repoLanguage.Trim().ToLower()));

            if (isPublic.HasValue)
                query = query.Where(x => x.IsPublic == isPublic.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => ToDto(x))
                .ToListAsync();

            return new PagedResult<AnalysisRunDto>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }

        // ─── GET by ID ────────────────────────────────────────────────────────────

        public async Task<AnalysisRunDto?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            return entity is null ? null : ToDto(entity);
        }

        // ─── CREATE ───────────────────────────────────────────────────────────────

        public async Task<AnalysisRunDto> CreateAsync(CreateAnalysisRunRequest request)
        {
            var entity = new AnalysisRun
            {
                Id              = Guid.NewGuid(),
                RepositoryPath  = request.RepositoryPath.Trim(),
                CreatedAt       = DateTime.UtcNow,
                RepoName        = request.RepoName?.Trim(),
                RepoOwner       = request.RepoOwner?.Trim(),
                RepoDescription = request.RepoDescription?.Trim(),
                RepoUrl         = request.RepoUrl?.Trim(),
                RepoLanguage    = request.RepoLanguage?.Trim(),
                RepoStars       = request.RepoStars,
                IsPublic        = request.IsPublic,
                RepoUpdatedAt   = request.RepoUpdatedAt
            };

            await _unitOfWork.AnalysisRuns.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(entity);
        }

        // ─── UPDATE ───────────────────────────────────────────────────────────────

        public async Task<AnalysisRunDto?> UpdateAsync(Guid id, UpdateAnalysisRunRequest request)
        {
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null) return null;

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

            return ToDto(entity);
        }

        // ─── DELETE ───────────────────────────────────────────────────────────────

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null) return false;

            _unitOfWork.AnalysisRuns.Delete(entity);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        // ─── Private mapper ───────────────────────────────────────────────────────

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





