using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.Code;
using Repo_Into_Graph_Application.Dtos.Business;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/businesses")]
    public class BusinessController : ControllerBase
    {
        private readonly ICodeQueryable _codeQueryable;
        private readonly IWorkflowAssessmentService _workflowAssessmentService;

        public BusinessController(
            ICodeQueryable codeQueryable,
            IWorkflowAssessmentService workflowAssessmentService)
        {
            _codeQueryable = codeQueryable ?? throw new ArgumentNullException(nameof(codeQueryable));
            _workflowAssessmentService = workflowAssessmentService ?? throw new ArgumentNullException(nameof(workflowAssessmentService));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BusinessViewDto>>> GetAll([FromQuery] Guid analysisRunId)
        {
            if (analysisRunId == Guid.Empty)
                return BadRequest(new { message = "analysisRunId query parameter là bắt buộc." });

            var businesses = await _codeQueryable.GetBusinessesByAnalysisRunIdAsync(analysisRunId);
            return Ok(businesses);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<BusinessViewDto>> GetById(Guid id)
        {
            var business = await _codeQueryable.GetBusinessByIdAsync(id);
            if (business == null)
            {
                return NotFound(new { message = $"Không tìm thấy Business với ID: {id}" });
            }
            return Ok(business);
        }

        [HttpGet("{id:guid}/codeflow")]
        public async Task<ActionResult<CodeFlowDto>> GetCodeFlow(Guid id)
        {
            var codeFlow = await _codeQueryable.GetCodeFlowAsync(id);
            if (codeFlow == null)
            {
                return NotFound(new { message = $"Không tìm thấy Code Flow của Business với ID: {id}" });
            }
            return Ok(codeFlow);
        }

        [HttpGet("{businessId:guid}/graph")]
        public async Task<ActionResult<BusinessWorkflowGraphDto>> GetGraph(Guid businessId)
        {
            var business = await _codeQueryable.GetBusinessByIdAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = $"Không tìm thấy Business với ID: {businessId}" });
            }

            var graph = await _workflowAssessmentService.GetBusinessWorkflowGraphAsync(businessId);
            return Ok(graph);
        }
    }
}
