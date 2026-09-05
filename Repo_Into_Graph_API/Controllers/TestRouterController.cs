using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;
using Repo_Into_Graph_Application.Services.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Services.HybridContextGenerator;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestRouterController : ControllerBase
    {
        private readonly IAdaptiveContextRouterService _routerService;
        private readonly IHybridContextGeneratorService _hybridContextService;

        public TestRouterController(
            IAdaptiveContextRouterService routerService,
            IHybridContextGeneratorService hybridContextService)
        {
            _routerService = routerService;
            _hybridContextService = hybridContextService;
        }

        /// <summary>
        /// [Tang 1] Phan tich code va quyet dinh dinh tuyen (ROUTE_RAW_CODE / ROUTE_HYBRID).
        /// </summary>
        [HttpPost("test-router")]
        public async Task<IActionResult> TestRouter([FromBody] RouterRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be null.");
            }

            var decision = await _routerService.EvaluateCodeContextAsync(request);
            return Ok(decision);
        }

        /// <summary>
        /// [Tang 2] Nhan HybridContextInput da duoc dong goi san, goi Tang 2 xu ly.
        /// Dung cho tool test ban giao (Checklist_Test_Tang_2_Handover.xlsx).
        /// </summary>
        [HttpPost("test-hybrid-context")]
        public async Task<IActionResult> TestHybridContext([FromBody] HybridContextInputDto input)
        {
            if (input == null)
            {
                return BadRequest("Request body cannot be null.");
            }

            var result = await _hybridContextService.GenerateAsync(input);
            return Ok(result);
        }
    }
}
