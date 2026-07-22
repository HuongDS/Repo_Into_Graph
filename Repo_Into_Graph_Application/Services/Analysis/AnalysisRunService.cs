using AutoMapper;
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
        private readonly IMapper _mapper;

        public AnalysisRunService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<PagedResult<AnalysisRunDto>> GetPagedAsync(
            int page,
            int pageSize,
            string? repoOwner,
            string? repoName,
            string? repoLanguage,
            bool? isPublic)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _unitOfWork.AnalysisRuns.GetPagedAnalysisRunsAsync(page, pageSize, repoOwner, repoName, repoLanguage, isPublic);

            var items = result.Items
                .Select(x => _mapper.Map<AnalysisRunDto>(x))
                .ToList();

            return new PagedResult<AnalysisRunDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.TotalCount
            };
        }

        public async Task<AnalysisRunDto> CreateAsync(CreateAnalysisRunRequest request)
        {
            var entity = _mapper.Map<AnalysisRun>(request);
            await _unitOfWork.AnalysisRuns.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<AnalysisRunDto>(entity);
        }

        public async Task<AnalysisRunDto?> UpdateAsync(Guid id, UpdateAnalysisRunRequest request)
        {
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null) throw new ArgumentException($"Không tìm thấy AnalysisRun với ID: {id}");

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

            return _mapper.Map<AnalysisRunDto>(entity);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null) throw new ArgumentException($"Không tìm thấy AnalysisRun với ID: {id}");
            _unitOfWork.AnalysisRuns.Delete(entity);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<AnalysisRunDto?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.AnalysisRuns.GetByIdAsync(id);
            if (entity is null) throw new ArgumentException($"Không tìm thấy AnalysisRun với ID: {id}");
            return _mapper.Map<AnalysisRunDto>(entity);
        }
    }
}





