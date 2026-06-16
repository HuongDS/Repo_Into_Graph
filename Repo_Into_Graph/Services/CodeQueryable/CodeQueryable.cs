using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using Repo_Into_Graph.Repo_Into_Graph.Mappings;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Code;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Feature;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.CodeQueryable
{
    public class CodeQueryable : ICodeQueryable
    {
        private readonly IFeatureRepository _featureRecordRepo;
        private readonly AnalysisDbContext _context;

        public CodeQueryable(IFeatureRepository featureRecordRepo, AnalysisDbContext context)
        {
            _featureRecordRepo = featureRecordRepo ?? throw new ArgumentNullException(nameof(featureRecordRepo));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<FeatureViewDto>> GetMethodNamesAsync(Guid? id)
        {
            if (id != null)
            {
                var record = await _featureRecordRepo.GetByIdAsync(id.Value);
                if (record == null) return Enumerable.Empty<FeatureViewDto>();

                return new List<FeatureViewDto> { record.ToDto() };
            }

            var res = await _featureRecordRepo.GetAllAsync();
            return res.Select(r => r.ToDto());
        }

        public async Task<CodeFlowDto?> GetCodeFlowAsync(Guid featureId)
        {
            var feature = await _context.FeatureRecords
                .Include(f => f.FeatureMethodMappings)
                .ThenInclude(fmm => fmm.MethodSource)
                .FirstOrDefaultAsync(f => f.Id == featureId);

            if (feature == null) return null;

            return new CodeFlowDto
            {
                Feature = feature.ToDto(),
                Methods = feature.FeatureMethodMappings
                    .Where(fmm => fmm.MethodSource != null)
                    .Select(fmm => fmm.MethodSource!.ToDto())
                    .ToList()
            };
        }
    }
}
