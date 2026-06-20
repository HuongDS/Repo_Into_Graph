using Repo_Into_Graph_DataAccess.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_Application.Mappings;
using Repo_Into_Graph_Application.Dtos.Code;
using Repo_Into_Graph_Application.Dtos.Business;

namespace Repo_Into_Graph_Application.Services.CodeQueryable
{
    public class CodeQueryable : ICodeQueryable
    {
        private readonly IBusinessRepository _businessRepo;
        private readonly AnalysisDbContext _context;

        public CodeQueryable(IBusinessRepository businessRepo, AnalysisDbContext context)
        {
            _businessRepo = businessRepo ?? throw new ArgumentNullException(nameof(businessRepo));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<BusinessViewDto>> GetBusinessesAsync(Guid? id)
        {
            if (id != null)
            {
                var record = await _businessRepo.GetByIdAsync(id.Value);
                if (record == null) return Enumerable.Empty<BusinessViewDto>();

                return new List<BusinessViewDto> { record.ToDto() };
            }

            var res = await _businessRepo.GetAllAsync();
            return res.Select(r => r.ToDto());
        }

        public async Task<CodeFlowDto?> GetCodeFlowAsync(Guid businessId)
        {
            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null) return null;

            var featureBusinessMappings = await _context.FeatureBusinessMappings
                .Where(m => m.BusinessId == businessId)
                .Select(m => m.FeatureId)
                .ToListAsync();

            var featureMethodMappings = await _context.FeatureMethodMappings
                .Include(m => m.MethodSource)
                .Where(m => featureBusinessMappings.Contains(m.FeatureId))
                .ToListAsync();

            var methods = featureMethodMappings
                .Where(m => m.MethodSource != null)
                .Select(m => m.MethodSource!)
                .DistinctBy(m => m.Id)
                .ToList();

            return new CodeFlowDto
            {
                Business = business.ToDto(),
                Methods = methods.Select(m => m.ToDto()).ToList()
            };
        }
    }
}
