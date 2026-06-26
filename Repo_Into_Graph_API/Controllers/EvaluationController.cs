using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_Application.Exceptions;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EvaluationController : Controller
    {
        //[HttpPost]
        //public async Task<IActionResult> GenerateUnifiedQuestions([FromBody] GenerateQuestionsRequest request)
        //{
        //    if (request.NumberOfQuestions <= 0)
        //        throw new BadRequestException("numberOfQuestions phải lớn hơn 0.");

        //    var result = null;
        //    return Ok(result);
        //}

    }
}
