using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Repo_Into_Graph.Models.FewShot;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FewShotController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public FewShotController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <summary>
        /// Lấy toàn bộ câu hỏi mẫu few-shot.
        /// GET /api/fewshot
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var examples = await _unitOfWork.FewShotExamples.GetAllAsync();
            return Ok(examples);
        }

        /// <summary>
        /// Lấy câu hỏi mẫu theo ID.
        /// GET /api/fewshot/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var example = await _unitOfWork.FewShotExamples.GetByIdAsync(id);
            if (example == null)
                return NotFound(new { error = $"Không tìm thấy few-shot example với ID: {id}" });
            return Ok(example);
        }

        /// <summary>
        /// Thêm câu hỏi mẫu mới.
        /// POST /api/fewshot
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FewShotExampleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Question không được để trống." });
            if (string.IsNullOrWhiteSpace(request.SuggestedAnswer))
                return BadRequest(new { error = "SuggestedAnswer không được để trống." });
            if (string.IsNullOrWhiteSpace(request.Difficulty))
                return BadRequest(new { error = "Difficulty không được để trống." });

            var entity = new FewShotExample
            {
                Id              = Guid.NewGuid(),
                Question        = request.Question.Trim(),
                SuggestedAnswer = request.SuggestedAnswer.Trim(),
                Difficulty      = request.Difficulty.Trim(),
                Tag             = request.Tag?.Trim(),
                Description     = request.Description?.Trim(),
                CreatedAt       = DateTime.UtcNow
            };

            await _unitOfWork.FewShotExamples.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        /// <summary>
        /// Cập nhật câu hỏi mẫu.
        /// PUT /api/fewshot/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FewShotExampleRequest request)
        {
            var entity = await _unitOfWork.FewShotExamples.GetByIdAsync(id);
            if (entity == null)
                return NotFound(new { error = $"Không tìm thấy few-shot example với ID: {id}" });

            if (!string.IsNullOrWhiteSpace(request.Question))
                entity.Question = request.Question.Trim();
            if (!string.IsNullOrWhiteSpace(request.SuggestedAnswer))
                entity.SuggestedAnswer = request.SuggestedAnswer.Trim();
            if (!string.IsNullOrWhiteSpace(request.Difficulty))
                entity.Difficulty = request.Difficulty.Trim();
            entity.Tag         = request.Tag?.Trim();
            entity.Description = request.Description?.Trim();

            _unitOfWork.FewShotExamples.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            return Ok(entity);
        }

        /// <summary>
        /// Xóa câu hỏi mẫu.
        /// DELETE /api/fewshot/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var entity = await _unitOfWork.FewShotExamples.GetByIdAsync(id);
            if (entity == null)
                return NotFound(new { error = $"Không tìm thấy few-shot example với ID: {id}" });

            _unitOfWork.FewShotExamples.Delete(entity);
            await _unitOfWork.SaveChangesAsync();

            return NoContent();
        }
    }

    public record FewShotExampleRequest(
        string Question,
        string SuggestedAnswer,
        string Difficulty,
        string? Tag,
        string? Description);
}
