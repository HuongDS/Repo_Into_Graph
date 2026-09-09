using System.Collections.Generic;
using Repo_Into_Graph_Application.Dtos.LLMOrchestrator;

namespace Repo_Into_Graph_Application.Services.LLMOrchestrator
{
    public interface IContextAggregatorService
    {
        string AggregateContext(IEnumerable<FunctionNode> nodes);
    }
}
