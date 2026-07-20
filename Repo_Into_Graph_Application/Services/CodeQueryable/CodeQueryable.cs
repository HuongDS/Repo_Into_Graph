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
        private readonly IBusinessRepository _businessRepository;
        private readonly IFeatureBusinessMappingRepository _featureBusinessMappingRepository;
        private readonly IFeatureMethodMappingRepository _featureMethodMappingRepository;

        public CodeQueryable(
            IBusinessRepository businessRepository,
            IFeatureBusinessMappingRepository featureBusinessMappingRepository,
            IFeatureMethodMappingRepository featureMethodMappingRepository)
        {
            _businessRepository = businessRepository ?? throw new ArgumentNullException(nameof(businessRepository));
            _featureBusinessMappingRepository = featureBusinessMappingRepository ?? throw new ArgumentNullException(nameof(featureBusinessMappingRepository));
            _featureMethodMappingRepository = featureMethodMappingRepository ?? throw new ArgumentNullException(nameof(featureMethodMappingRepository));
        }

        public async Task<IEnumerable<BusinessViewDto>> GetBusinessesByAnalysisRunIdAsync(Guid analysisRunId)
        {
            var res = await _businessRepository.FindAsync(b => b.AnalysisRunId == analysisRunId);
            return res.Select(r => r.ToDto());
        }

        public async Task<BusinessViewDto?> GetBusinessByIdAsync(Guid id)
        {
            var record = await _businessRepository.GetByIdAsync(id);
            return record?.ToDto();
        }

        public async Task<CodeFlowDto?> GetCodeFlowAsync(Guid businessId)
        {
            var business = await _businessRepository.GetByIdAsync(businessId);

            if (business == null) return null;

            var featureBusinessMappings = await _featureBusinessMappingRepository.GetFeatureIdsByBusinessIdAsync(businessId);

            var featureMethodMappings = await _featureMethodMappingRepository.GetMappingsWithMethodSourceByFeatureIdsAsync(featureBusinessMappings);

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
