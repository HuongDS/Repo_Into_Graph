using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.Code;
using Repo_Into_Graph_Application.Dtos.Business;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Services.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/businesses")]
    public class BusinessController : ControllerBase
    {
        private readonly ICodeQueryable _codeQueryable;
        private readonly IWorkflowAssessmentService _workflowAssessmentService;
        private readonly IAdaptiveContextRouterService _routerService;

        public BusinessController(
            ICodeQueryable codeQueryable,
            IWorkflowAssessmentService workflowAssessmentService,
            IAdaptiveContextRouterService routerService)
        {
            _codeQueryable = codeQueryable ?? throw new ArgumentNullException(nameof(codeQueryable));
            _workflowAssessmentService = workflowAssessmentService ?? throw new ArgumentNullException(nameof(workflowAssessmentService));
            _routerService = routerService ?? throw new ArgumentNullException(nameof(routerService));
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

        [HttpGet("{businessId:guid}/hybrid-context")]
        public async Task<IActionResult> GetHybridContext(Guid businessId)
        {
            var business = await _codeQueryable.GetBusinessByIdAsync(businessId);
            if (business == null)
            {
                return NotFound(new { message = $"Không tìm thấy Business với ID: {businessId}" });
            }

            var codeFlow = await _codeQueryable.GetCodeFlowAsync(businessId);
            if (codeFlow == null || codeFlow.Methods == null || !codeFlow.Methods.Any())
            {
                return NotFound(new { message = $"Không tìm thấy Code Flow của Business với ID: {businessId}" });
            }

            var sourceCodeBuilder = new StringBuilder();
            foreach (var m in codeFlow.Methods)
            {
                sourceCodeBuilder.AppendLine($"// Lớp: {m.ClassName} | Hàm: {m.MethodName}");
                sourceCodeBuilder.AppendLine(m.SourceCode);
                sourceCodeBuilder.AppendLine();
            }

            var request = new RouterRequestDto
            {
                ModuleId = business.BusinessName,
                SourceCode = sourceCodeBuilder.ToString(),
                Language = "" // Để Router tự phân tích
            };

            var decision = await _routerService.EvaluateCodeContextAsync(request);
            if (decision.SelectedRoute != RoutingType.HybridGraph || decision.HybridContextResult == null)
            {
                // Fallback nếu code quá ngắn
                return BadRequest(new { message = "Mã nguồn không đủ độ phức tạp để tạo Hybrid Graph (được định tuyến qua RawCode)." });
            }

            return Ok(decision.HybridContextResult);
        }
    }
}
