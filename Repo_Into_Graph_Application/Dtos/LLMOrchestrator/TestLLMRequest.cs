using System.Collections.Generic;

namespace Repo_Into_Graph_Application.Dtos.LLMOrchestrator
{
    public class TestLLMRequest
    {
        public string WorkflowOverview { get; set; } = string.Empty;
        public List<FunctionNode> Nodes { get; set; } = new List<FunctionNode>();
    }
}
