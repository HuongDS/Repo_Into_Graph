using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.BusinessFlow;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.BusinessFlows
{
    public class BusinessFlowService : IBusinessFlowService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BusinessFlowService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        // ─── GET paged ────────────────────────────────────────────────────────────

        public async Task<BusinessFlowPagedResult> GetPagedAsync(
            int page,
            int pageSize,
            Guid? analysisRunId,
            string? name)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            IQueryable<Models.BusinessFlows.BusinessFlow> query = _unitOfWork.BusinessFlows
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
                .Select(x => new BusinessFlowSummaryDto
                {
                    Id            = x.Id,
                    AnalysisRunId = x.AnalysisRunId,
                    Name          = x.Name,
                    EntryPoint    = x.EntryPoint,
                    StepCount     = x.Steps.Count,
                    CreatedAt     = x.CreatedAt
                })
                .ToListAsync();

            return new BusinessFlowPagedResult
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }

        // ─── GET all by analysisRunId ─────────────────────────────────────────────

        public async Task<IEnumerable<BusinessFlowDetailDto>> GetAllByAnalysisRunAsync(Guid analysisRunId)
        {
            var flows = await _unitOfWork.BusinessFlows
                .AsQueryable()
                .Where(x => x.AnalysisRunId == analysisRunId)
                .Include(x => x.Steps)
                .OrderBy(x => x.Name)
                .ToListAsync();

            return flows.Select(ToDetailDto).ToList();
        }

        // ─── GET by ID ────────────────────────────────────────────────────────────

        public async Task<BusinessFlowDetailDto?> GetByIdAsync(Guid id)
        {
            var flow = await _unitOfWork.BusinessFlows
                .AsQueryable()
                .Include(x => x.Steps)
                .FirstOrDefaultAsync(x => x.Id == id);

            return flow is null ? null : ToDetailDto(flow);
        }

        // ─── Private mappers ──────────────────────────────────────────────────────

        private static BusinessFlowDetailDto ToDetailDto(Models.BusinessFlows.BusinessFlow flow) => new()
        {
            Id                  = flow.Id,
            AnalysisRunId       = flow.AnalysisRunId,
            Name                = flow.Name,
            EntryPoint          = flow.EntryPoint,
            CreatedAt           = flow.CreatedAt,
            MermaidGraph        = flow.MermaidGraph,
            DataFlowMermaidGraph = flow.DataFlowMermaidGraph,
            Steps = flow.Steps
                .OrderBy(s => s.StepOrder)
                .Select(ToStepDto)
                .ToList()
        };

        private static BusinessFlowStepDto ToStepDto(BusinessFlowStep s) => new()
        {
            Id             = s.Id,
            BusinessFlowId = s.BusinessFlowId,
            StepOrder      = s.StepOrder,
            CallerClass    = s.CallerClass,
            CallerMethod   = s.CallerMethod,
            CalleeClass    = s.CalleeClass,
            CalleeMethod   = s.CalleeMethod,
            CreatedAt      = s.CreatedAt
        };
    }
}
