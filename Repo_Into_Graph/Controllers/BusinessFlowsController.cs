using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.BusinessFlow;
using Repo_Into_Graph.Repo_Into_Graph.Services.BusinessFlows;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    /// <summary>
    /// API cho Business Flows.
    /// Base route: /api/business-flows
    /// </summary>
    [ApiController]
    [Route("api/business-flows")]
    public class BusinessFlowsController : ControllerBase
    {
        private readonly IBusinessFlowService _businessFlowService;

        public BusinessFlowsController(IBusinessFlowService businessFlowService)
        {
            _businessFlowService = businessFlowService
                ?? throw new ArgumentNullException(nameof(businessFlowService));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/business-flows?page=1&pageSize=10&analysisRunId=...&name=...
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// used for listing
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<BusinessFlowPagedResult>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? analysisRunId = null,
            [FromQuery] string? name = null)
        {
            var result = await _businessFlowService.GetPagedAsync(
                page, pageSize, analysisRunId, name);

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/business-flows/by-analysis-run/{analysisRunId}
        // ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Include mermaid
        /// </summary>
        [HttpGet("by-analysis-run/{analysisRunId:guid}")]
        public async Task<ActionResult<IEnumerable<BusinessFlowDetailDto>>> GetByAnalysisRun(Guid analysisRunId)
        {
            var flows = await _businessFlowService.GetAllByAnalysisRunAsync(analysisRunId);
            return Ok(flows);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/business-flows/{id}
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<BusinessFlowDetailDto>> GetById(Guid id)
        {
            var dto = await _businessFlowService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy Business Flow với ID: {id}" });

            return Ok(dto);
        }
    }
}
