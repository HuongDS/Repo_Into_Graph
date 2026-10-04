using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.StudentAnswerEvaluation;
using Repo_Into_Graph_Application.Services.StudentAnswerEvaluation;
using Repo_Into_Graph_Application.Exceptions;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/student-evaluation")]
    public class StudentEvaluationController : ControllerBase
    {
        private readonly IStudentEvaluationService _evaluationService;

        public StudentEvaluationController(IStudentEvaluationService evaluationService)
        {
            _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        }

        [HttpPost("evaluate")]
        public async Task<IActionResult> EvaluateStudentAnswer([FromBody] StudentEvaluationRequestDto request)
        {
            if (request == null)
            {
                throw new BadRequestException("Dữ liệu gửi lên không được để trống.");
            }

            if (request.BusinessId == Guid.Empty)
            {
                throw new BadRequestException("Thiếu BusinessId để hệ thống tìm mã nguồn.");
            }

            if (string.IsNullOrWhiteSpace(request.StudentAnswer))
            {
                throw new BadRequestException("Câu trả lời của sinh viên không được để trống.");
            }

            var jsonResult = await _evaluationService.EvaluateStudentAnswerAsync(request);

            return Content(jsonResult, "application/json");
        }
    }
}
