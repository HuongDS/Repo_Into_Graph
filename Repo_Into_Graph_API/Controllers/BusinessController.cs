using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.Code;
using Repo_Into_Graph_Application.Dtos.Business;

namespace Repo_Into_Graph_API.Controllers
{
    [ApiController]
    [Route("api/businesses")]
    public class BusinessController : ControllerBase
    {
        private readonly ICodeQueryable _codeQueryable;

        public BusinessController(ICodeQueryable codeQueryable)
        {
            _codeQueryable = codeQueryable ?? throw new ArgumentNullException(nameof(codeQueryable));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BusinessViewDto>>> GetAll()
        {
            var businesses = await _codeQueryable.GetBusinessesAsync(null);
            return Ok(businesses);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<BusinessViewDto>> GetById(Guid id)
        {
            var businesses = await _codeQueryable.GetBusinessesAsync(id);
            var business = businesses.FirstOrDefault();
            if (business == null)
            {
                return NotFound(new { message = $"Không tìm thấy Business với ID: {id}" });
            }
            return Ok(business);
        }

        [HttpGet("{id:guid}/codeflow")]
        public async Task<ActionResult<CodeFlowDto>> GetCodeFlow(Guid id)
        {
            var codeFlow = await _codeQueryable.GetCodeFlowAsync(id);
            if (codeFlow == null)
            {
                return NotFound(new { message = $"Không tìm thấy Code Flow của Business với ID: {id}" });
            }
            return Ok(codeFlow);
        }
    }
}
