using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.QuestionGenerate;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionGeneratorController : ControllerBase
    {
        private readonly IQuestionGenerate _questionGenerate;

        public QuestionGeneratorController(IQuestionGenerate questionGenerate)
        {
            _questionGenerate = questionGenerate ?? throw new ArgumentNullException(nameof(questionGenerate));
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateQuestions([FromBody] GenerateQuestionsRequest request)
        {
            var result = await _questionGenerate.GenerateQuestionsAsync(request);
            return Ok(result);
        }

        [HttpPost("generate/from-business-flow")]
        public async Task<IActionResult> GenerateQuestionsFromBusinessFlow(
            [FromBody] GenerateQuestionsFromFlowRequest request)
        {
            if (request.NumberOfQuestions <= 0)
                throw new BadRequestException("numberOfQuestions phải lớn hơn 0.");

            var result = await _questionGenerate.GenerateQuestionsFromFlowAsync(request);
            return Ok(result);
        }
    }
}



