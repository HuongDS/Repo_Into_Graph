using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.QuestionGenerate;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using System;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionGeneratorController : ControllerBase
    {
        private readonly IQuestionGenerate _questionGenerate;
        private readonly IWorkflowAssessmentService _workflowAssessmentService;

        public QuestionGeneratorController(
            IQuestionGenerate questionGenerate,
            IWorkflowAssessmentService workflowAssessmentService)
        {
            _questionGenerate = questionGenerate ?? throw new ArgumentNullException(nameof(questionGenerate));
            _workflowAssessmentService = workflowAssessmentService ?? throw new ArgumentNullException(nameof(workflowAssessmentService));
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
    }
}



