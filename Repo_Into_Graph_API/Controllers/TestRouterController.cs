using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Services.AdaptiveContextRouter;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestRouterController : ControllerBase
    {
        private readonly IAdaptiveContextRouterService _routerService;

        public TestRouterController(IAdaptiveContextRouterService routerService)
        {
            _routerService = routerService;
        }

        [HttpPost("test-router")]
        public async Task<IActionResult> TestRouter([FromBody] RouterRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be null.");
            }

            var decision = await _routerService.EvaluateCodeContextAsync(request);
            
            // Nếu ngôn ngữ không hỗ trợ hoặc lỗi hệ thống, IsValidSyntax có thể vẫn = false kèm message
            // Ở đây trả về OK kèm JSON chi tiết để Client (Benchmark) tự Assert
            return Ok(decision);
        }
    }
}
