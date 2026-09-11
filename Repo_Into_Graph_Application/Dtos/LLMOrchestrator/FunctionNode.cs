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

        /// <summary>
        /// Ngu canh lai da toi uu tu Tang 2 (CFG skeleton + critical decision logic
        /// da loc theo trong so + enriched metadata) - dung TRUC TIEP cho Tang 3
        /// thay vi tu ghep CfgSkeleton + CriticalSnippets (se lam phinh to token).
        /// Chi co gia tri khi RouteType == "ROUTE_HYBRID".
        /// </summary>
        public string HybridPrompt { get; set; } = string.Empty;
    }
}
