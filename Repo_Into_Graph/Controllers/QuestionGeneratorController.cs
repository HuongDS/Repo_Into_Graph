using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Repo_Into_Graph.Services.AI;
using Repo_Into_Graph.Repo_Into_Graph.Models.FewShot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;
using Repo_Into_Graph.Repo_Into_Graph.Services.QuestionGenerate;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionGeneratorController : ControllerBase
    {
        private readonly IQuestionGenerate _questionGenerate;
        private readonly IAIService _aiService;
        private readonly AnalysisDbContext _context;

        public QuestionGeneratorController(
            IQuestionGenerate questionGenerate,
            IAIService aiService,
            AnalysisDbContext context)
        {
            _questionGenerate = questionGenerate;
            _aiService       = aiService  ?? throw new ArgumentNullException(nameof(aiService));
            _context         = context    ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Sinh câu hỏi dựa trên Feature (cách cũ).
        /// POST /api/questiongenerator/generate
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateQuestions([FromBody] GenerateQuestionsRequest request)
        {
            try
            {
                var result = await _questionGenerate.GenerateQuestionsAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)       { return BadRequest(new { error = ex.Message }); }
            catch (KeyNotFoundException ex)    { return NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
            catch (Exception ex)               { return StatusCode(500, new { error = $"System error: {ex.Message}" }); }
        }

        /// <summary>
        /// Sinh câu hỏi vấn đáp dựa trên một Business Flow cụ thể.
        /// POST /api/questiongenerator/generate/from-business-flow
        /// Body: { "businessFlowId": "...", "numberOfQuestions": 5, "difficulty": "Medium", "additionalContext": "...", "fewShotExampleIds": ["id1", "id2"] }
        /// </summary>
        [HttpPost("generate/from-business-flow")]
        public async Task<IActionResult> GenerateQuestionsFromBusinessFlow(
            [FromBody] GenerateQuestionsFromFlowRequest request)
        {
            if (request.NumberOfQuestions <= 0)
                return BadRequest(new { error = "numberOfQuestions phải lớn hơn 0." });

            try
            {
                // Load BusinessFlow cùng Steps từ DB
                var businessFlow = await _context.BusinessFlows
                    .Include(f => f.Steps)
                    .FirstOrDefaultAsync(f => f.Id == request.BusinessFlowId);

                if (businessFlow == null)
                    return NotFound(new { error = $"Không tìm thấy Business Flow với ID: {request.BusinessFlowId}" });

                // Load few-shot examples: ưu tiên theo danh sách ID, nếu không có thì lọc theo difficulty
                IEnumerable<FewShotExample>? fewShotExamples = null;

                if (request.FewShotExampleIds != null && request.FewShotExampleIds.Count > 0)
                {
                    var ids = request.FewShotExampleIds;
                    fewShotExamples = await _context.FewShotExamples
                        .Where(e => ids.Contains(e.Id))
                        .ToListAsync();
                }
                else if (!string.IsNullOrWhiteSpace(request.Difficulty))
                {
                    // Tự động lấy các ví dụ cùng mức độ khó (tối đa 5)
                    fewShotExamples = await _context.FewShotExamples
                        .Where(e => e.Difficulty.ToLower() == request.Difficulty.ToLower())
                        .Take(5)
                        .ToListAsync();
                }

                var questions = await _aiService.GenerateQuestionsFromBusinessFlowAsync(
                    businessFlow      : businessFlow,
                    numberOfQuestions : request.NumberOfQuestions,
                    difficulty        : request.Difficulty,
                    additionalContext : request.AdditionalContext,
                    fewShotExamples   : fewShotExamples);

                return Ok(new
                {
                    BusinessFlowId   = businessFlow.Id,
                    BusinessFlowName = businessFlow.Name,
                    EntryPoint       = businessFlow.EntryPoint,
                    TotalSteps       = businessFlow.Steps?.Count ?? 0,
                    FewShotUsed      = fewShotExamples?.Count() ?? 0,
                    Questions        = questions
                });
            }
            catch (ArgumentException ex)         { return BadRequest(new { error = ex.Message }); }
            catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
            catch (Exception ex)                 { return StatusCode(500, new { error = $"System error: {ex.Message}" }); }
        }
    }
}
