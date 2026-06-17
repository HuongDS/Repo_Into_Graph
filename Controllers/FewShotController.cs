using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.FewShot;
using Repo_Into_Graph_Application.Dtos.FewShot;
using System;
using System.Threading.Tasks;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/fewshot")]
    public class FewShotController : ControllerBase
    {
        private readonly IFewShotService _fewShotService;

        public FewShotController(IFewShotService fewShotService)
        {
            _fewShotService = fewShotService
                ?? throw new ArgumentNullException(nameof(fewShotService));
        }

        [HttpGet]
        public async Task<ActionResult<FewShotPagedResult>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? difficulty = null,
            [FromQuery] string? tag = null)
        {
            var result = await _fewShotService.GetPagedAsync(page, pageSize, difficulty, tag);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<FewShotExampleDto>> GetById(Guid id)
        {
            var dto = await _fewShotService.GetByIdAsync(id);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy few-shot example với ID: {id}" });

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<FewShotExampleDto>> Create(
            [FromBody] CreateFewShotExampleRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = await _fewShotService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<FewShotExampleDto>> Update(
            Guid id,
            [FromBody] UpdateFewShotExampleRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = await _fewShotService.UpdateAsync(id, request);
            if (dto is null)
                return NotFound(new { error = $"Không tìm thấy few-shot example với ID: {id}" });

            return Ok(dto);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _fewShotService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { error = $"Không tìm thấy few-shot example với ID: {id}" });

            return NoContent();
        }
    }
}



