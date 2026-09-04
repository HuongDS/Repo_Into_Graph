using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_Application.Dtos.Feature;
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Features
{
    public class FeatureService : IFeatureService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FeatureService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<FeaturePagedResult> GetPagedAsync(
            int page,
            int pageSize,
            Guid? analysisRunId,
            string? name)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            IQueryable<Feature> query = _unitOfWork.Features
                .AsQueryable()
                .OrderByDescending(x => x.CreatedAt);

            if (analysisRunId.HasValue)
                query = query.Where(x => x.AnalysisRunId == analysisRunId.Value);

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(x => x.Name.ToLower().Contains(name.Trim().ToLower()));

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => _mapper.Map<FeatureSummaryDto>(x))
                .ToListAsync();

            return new FeaturePagedResult
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<IEnumerable<FeatureDetailDto>> GetAllByAnalysisRunAsync(Guid analysisRunId)
        {
            var features = await _unitOfWork.Features
                .AsQueryable()
                .Where(x => x.AnalysisRunId == analysisRunId)
                .Include(x => x.Steps)
                .OrderBy(x => x.Name)
                .ToListAsync();

            return features.Select(f => _mapper.Map<FeatureDetailDto>(f)).ToList();
        }

        public async Task<FeatureDetailDto?> GetByIdAsync(Guid id)
        {
            var feature = await _unitOfWork.Features
                .AsQueryable()
                .Include(x => x.Steps)
                .FirstOrDefaultAsync(x => x.Id == id);

            return feature is null ? null : _mapper.Map<FeatureDetailDto>(feature);
        }
    }
}
