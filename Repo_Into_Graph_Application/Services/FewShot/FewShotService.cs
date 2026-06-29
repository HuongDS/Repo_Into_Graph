using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_Application.Dtos.FewShot;
using Repo_Into_Graph_Application.Enums;
using Repo_Into_Graph_Application.Exceptions;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.FewShot
{
    public class FewShotService : IFewShotService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FewShotService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        // ─── GET paged ────────────────────────────────────────────────────────────

        public async Task<FewShotPagedResult> GetPagedAsync(
            int page,
            int pageSize,
            string? difficulty,
            string? tag)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            IQueryable<FewShotExample> query = _unitOfWork.FewShotExamples
                .AsQueryable()
                .OrderByDescending(x => x.CreatedAt);

            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                if (Enum.TryParse<DifficultyLevel>(difficulty, true, out DifficultyLevel parsedDifficulty))
                {
                    query = query.Where(x => x.Difficulty == parsedDifficulty);
                }
            }

            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(x => x.Tag != null &&
                                         x.Tag.ToLower().Contains(tag.Trim().ToLower()));

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => ToDto(x))
                .ToListAsync();

            return new FewShotPagedResult
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }

        // ─── GET by ID ────────────────────────────────────────────────────────────

        public async Task<FewShotExampleDto?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.FewShotExamples.GetByIdAsync(id);
            return entity is null ? null : ToDto(entity);
        }

        // ─── CREATE ───────────────────────────────────────────────────────────────

        public async Task<FewShotExampleDto> CreateAsync(CreateFewShotExampleRequest request)
        {
            var entity = new FewShotExample
            {
                Id              = Guid.NewGuid(),
                Question        = request.Question.Trim(),
                SuggestedAnswer = request.SuggestedAnswer.Trim(),
                Difficulty      =  request.Difficulty,
                Tag             = request.Tag?.Trim(),
                Description     = request.Description?.Trim(),
                CreatedAt       = DateTime.UtcNow
            };

            await _unitOfWork.FewShotExamples.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(entity);
        }

        // ─── UPDATE ───────────────────────────────────────────────────────────────

        public async Task<FewShotExampleDto?> UpdateAsync(Guid id, UpdateFewShotExampleRequest request)
        {
            var entity = await _unitOfWork.FewShotExamples.GetByIdAsync(id);
            if (entity is null) return null;

            // Chỉ ghi đè khi giá trị được truyền lên (khác null)
            if (request.Question is not null)
                entity.Question = request.Question.Trim();
            if (request.SuggestedAnswer is not null)
                entity.SuggestedAnswer = request.SuggestedAnswer.Trim();
            if (request.Difficulty is not null)
                entity.Difficulty = request.Difficulty;
            if (request.Tag is not null)
                entity.Tag = request.Tag.Trim();
            if (request.Description is not null)
                entity.Description = request.Description.Trim();

            _unitOfWork.FewShotExamples.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(entity);
        }

        // ─── DELETE ───────────────────────────────────────────────────────────────

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.FewShotExamples.GetByIdAsync(id);
            if (entity is null) return false;

            _unitOfWork.FewShotExamples.Delete(entity);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        // ─── Private mapper ───────────────────────────────────────────────────────

        private static FewShotExampleDto ToDto(FewShotExample x) => new()
        {
            Id              = x.Id,
            Question        = x.Question,
            SuggestedAnswer = x.SuggestedAnswer,
            Difficulty      = x.Difficulty,
            Tag             = x.Tag,
            Description     = x.Description,
            CreatedAt       = x.CreatedAt
        };
    }
}





