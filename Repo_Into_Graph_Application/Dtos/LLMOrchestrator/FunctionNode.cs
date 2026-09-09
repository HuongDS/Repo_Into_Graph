namespace Repo_Into_Graph_Application.Dtos.LLMOrchestrator
{
    public class FunctionNode
    {
        public string NodeName { get; set; } = string.Empty;
        public string Language { get; set; } = "csharp";
        public string RouteType { get; set; } = "ROUTE_RAW_CODE";
        public string SourceCode { get; set; } = string.Empty;
        public string CfgSkeleton { get; set; } = string.Empty;
        public string CriticalSnippets { get; set; } = string.Empty;
    }
}
