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
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.Method;

namespace Repo_Into_Graph_Application.Services.CodeQueryable
{
    public class CodeQueryable : ICodeQueryable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CodeQueryable(
           IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<BusinessViewDto>> GetBusinessesByAnalysisRunIdAsync(Guid analysisRunId)
        {
            var res = await _unitOfWork.Businesses.FindAsync(b => b.AnalysisRunId == analysisRunId);
            return res.Select(r => _mapper.Map<BusinessViewDto>(r));
        }

        public async Task<BusinessViewDto?> GetBusinessByIdAsync(Guid id)
        {
            var record = await _unitOfWork.Businesses.GetByIdAsync(id);
            return _mapper.Map<BusinessViewDto?>(record);
        }

        public async Task<CodeFlowDto?> GetCodeFlowAsync(Guid businessId)
        {
            var business = await _unitOfWork.Businesses.GetByIdAsync(businessId);

            if (business == null) return null;

            var featureBusinessMappings = await _unitOfWork.FeatureBusinessMappings.GetFeatureIdsByBusinessIdAsync(businessId);
            var featureMethodMappings = await _unitOfWork.FeatureMethodMappings.GetMappingsWithMethodSourceByFeatureIdsAsync(featureBusinessMappings);

            var methods = featureMethodMappings
                .Where(m => m.MethodSource != null)
                .Select(m => m.MethodSource!)
                .DistinctBy(m => m.Id)
                .ToList();

            return new CodeFlowDto
            {
                Business = _mapper.Map<BusinessViewDto>(business),
                Methods = methods.Select(m => _mapper.Map<MethodSourceDto>(m)).ToList()
            };
        }
    }
}
