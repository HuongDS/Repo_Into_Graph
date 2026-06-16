using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;
using Repo_Into_Graph.Repo_Into_Graph.Services.QuestionGenerate;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionGeneratorController : ControllerBase
    {
        private readonly IQuestionGenerate _questionGenerate;

        public QuestionGeneratorController(IQuestionGenerate questionGenerate)
        {
            _questionGenerate = questionGenerate;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateQuestions([FromBody] GenerateQuestionsRequest request)
        {
            try
            {
                var result = await _questionGenerate.GenerateQuestionsAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"System error: {ex.Message}" });
            }
        }
    }
}
