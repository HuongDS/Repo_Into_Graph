using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;
using Repo_Into_Graph_Application.Services.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Services.HybridContextGenerator;
using Repo_Into_Graph_Application.Services.LLMOrchestrator;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Dtos.LLMOrchestrator;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestRouterController : ControllerBase
    {
        private readonly IAdaptiveContextRouterService _routerService;
        private readonly IHybridContextGeneratorService _hybridContextService;
        private readonly IContextAggregatorService _contextAggregatorService;
        private readonly IPromptBuilderService _promptBuilderService;
        private readonly IAIService _aiService;

        public TestRouterController(
            IAdaptiveContextRouterService routerService,
            IHybridContextGeneratorService hybridContextService,
            IContextAggregatorService contextAggregatorService,
            IPromptBuilderService promptBuilderService,
            IAIService aiService)
        {
            _routerService = routerService;
            _hybridContextService = hybridContextService;
            _contextAggregatorService = contextAggregatorService;
            _promptBuilderService = promptBuilderService;
            _aiService = aiService;
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

        /// <summary>
        /// [Tang 3] Nhận danh sách các Node, gom ngữ cảnh, build prompt và gọi Gemini sinh câu hỏi.
        /// </summary>
        [HttpPost("test-llm-orchestrator")]
        public async Task<IActionResult> TestLLMOrchestrator([FromBody] TestLLMRequest request)
        {
            if (request == null || request.Nodes == null)
            {
                return BadRequest("Request body cannot be null.");
            }

            // Bước 1: Gom Code thô và CFG
            string aggregatedContext = _contextAggregatorService.AggregateContext(request.Nodes);

            // Bước 2: Build toàn bộ Prompt
            string systemPrompt = _promptBuilderService.BuildSystemPrompt();
            string finalPayload = _promptBuilderService.BuildFinalPayload(request.WorkflowOverview, aggregatedContext);

            // Bước 3: Gọi AI
            var response = await _aiService.GenerateCodeQuestionsAsync(systemPrompt, finalPayload);
            
            return Ok(new
            {
                FinalPayload = finalPayload, // Trả về payload để tiện debug xem Tầng 3 ghép chuỗi có chuẩn không
                AIResponse = response
            });
        }
    }
}
