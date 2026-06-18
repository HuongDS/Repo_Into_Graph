using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.BusinessFlows;
using Repo_Into_Graph_Application.Dtos.BusinessFlow;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
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

        [HttpGet("by-analysis-run/{analysisRunId:guid}")]
        public async Task<ActionResult<IEnumerable<BusinessFlowDetailDto>>> GetByAnalysisRun(Guid analysisRunId)
        {
            var flows = await _businessFlowService.GetAllByAnalysisRunAsync(analysisRunId);
            return Ok(flows);
        }

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



