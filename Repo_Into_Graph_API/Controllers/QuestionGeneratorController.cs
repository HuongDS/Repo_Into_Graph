using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.QuestionGenerate;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using System;
using System.Threading.Tasks;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.LLMOrchestrator;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionGeneratorController : ControllerBase
    {
        private readonly IQuestionGenerate _questionGenerate;
        private readonly IWorkflowAssessmentService _workflowAssessmentService;
        private readonly IE2EOrchestratorService _e2eOrchestratorService;

        public QuestionGeneratorController(
            IQuestionGenerate questionGenerate,
            IWorkflowAssessmentService workflowAssessmentService,
            IE2EOrchestratorService e2eOrchestratorService)
        {
            _questionGenerate = questionGenerate ?? throw new ArgumentNullException(nameof(questionGenerate));
            _workflowAssessmentService = workflowAssessmentService ?? throw new ArgumentNullException(nameof(workflowAssessmentService));
            _e2eOrchestratorService = e2eOrchestratorService ?? throw new ArgumentNullException(nameof(e2eOrchestratorService));
        }

        [HttpPost("generate-traditional")]
        public async Task<IActionResult> GenerateTraditional([FromBody] GenerateQuestionsRequest request)
        {
            if (request.NumberOfQuestions <= 0)
                throw new BadRequestException("numberOfQuestions phải lớn hơn 0.");

            request.Mode = "Traditional";
            var result = await _questionGenerate.GenerateQuestionsAsync(request);
            return Ok(result);
        }

        [HttpPost("generate-graph")]
        public async Task<IActionResult> GenerateGraph([FromBody] GenerateQuestionsRequest request)
        {
            if (request.NumberOfQuestions <= 0)
                throw new BadRequestException("numberOfQuestions phải lớn hơn 0.");

            request.Mode = "Graph";
            var result = await _questionGenerate.GenerateQuestionsAsync(request);
            return Ok(result);
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateUnifiedQuestions([FromBody] GenerateQuestionsRequest request)
        {
            if (request.NumberOfQuestions <= 0)
                throw new BadRequestException("numberOfQuestions phải lớn hơn 0.");

            var result = await _questionGenerate.GenerateQuestionsAsync(request);
            return Ok(result);
        }

        [HttpPost("highlight-graph")]
        public async Task<IActionResult> HighlightGraph([FromBody] AssessmentRequestDto request)
        {
            if (request == null)
                throw new BadRequestException("Request body không được để trống.");

            if (string.IsNullOrWhiteSpace(request.Question))
                throw new BadRequestException("Trường 'question' không được để trống.");

            var result = await _workflowAssessmentService.Coverage.AssessAsync(request);
            return Ok(result);
        }

        [HttpPost("generate-e2e")]
        public async Task<IActionResult> GenerateE2EQuestions([FromBody] GenerateQuestionsRequest request)
        {
            if (request.NumberOfQuestions <= 0)
                throw new BadRequestException("numberOfQuestions phải lớn hơn 0.");

            var result = await _e2eOrchestratorService.GenerateE2EQuestionsAsync(request);
            return Ok(result);
        }
    }
}



