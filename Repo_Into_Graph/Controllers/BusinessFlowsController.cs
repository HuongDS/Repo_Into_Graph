using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessFlowsController : ControllerBase
    {
        private readonly AnalysisDbContext _context;

        public BusinessFlowsController(AnalysisDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAll()
        {
            var flows = await _context.BusinessFlows
                .Select(f => new
                {
                    f.Id,
                    f.AnalysisRunId,
                    f.Name,
                    f.EntryPoint,
                    f.CreatedAt
                })
                .ToListAsync();

            return Ok(flows);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<object>> GetById(Guid id)
        {
            var flow = await _context.BusinessFlows
                .Include(f => f.Steps)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (flow == null)
            {
                return NotFound(new { message = $"Không tìm thấy Business Flow với ID: {id}" });
            }

            var result = new
            {
                flow.Id,
                flow.AnalysisRunId,
                flow.Name,
                flow.EntryPoint,
                flow.MermaidGraph,
                flow.CreatedAt,
                Steps = flow.Steps.OrderBy(s => s.StepOrder).Select(s => new
                {
                    s.Id,
                    s.CallerClass,
                    s.CallerMethod,
                    s.CalleeClass,
                    s.CalleeMethod,
                    s.StepOrder
                }).ToList()
            };

            return Ok(result);
        }
    }
}
