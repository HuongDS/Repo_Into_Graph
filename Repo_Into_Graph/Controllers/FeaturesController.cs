using Microsoft.AspNetCore.Mvc;
using Repo_Into_Graph.Repo_Into_Graph.Services.CodeQueryable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Code;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Feature;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeaturesController : ControllerBase
    {
        private readonly ICodeQueryable _codeQueryable;

        public FeaturesController(ICodeQueryable codeQueryable)
        {
            _codeQueryable = codeQueryable ?? throw new ArgumentNullException(nameof(codeQueryable));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FeatureViewDto>>> GetAll()
        {
            var features = await _codeQueryable.GetMethodNamesAsync(null);
            return Ok(features);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<FeatureViewDto>> GetById(Guid id)
        {
            var features = await _codeQueryable.GetMethodNamesAsync(id);
            var feature = features.FirstOrDefault();
            if (feature == null)
            {
                return NotFound(new { message = $"Không tìm thấy Feature với ID: {id}" });
            }
            return Ok(feature);
        }

        [HttpGet("{id:guid}/codeflow")]
        public async Task<ActionResult<CodeFlowDto>> GetCodeFlow(Guid id)
        {
            var codeFlow = await _codeQueryable.GetCodeFlowAsync(id);
            if (codeFlow == null)
            {
                return NotFound(new { message = $"Không tìm thấy Code Flow của Feature với ID: {id}" });
            }
            return Ok(codeFlow);
        }
    }
}
