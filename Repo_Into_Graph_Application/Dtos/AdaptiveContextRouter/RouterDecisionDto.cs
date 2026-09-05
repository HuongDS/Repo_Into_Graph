namespace Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter
{
    public enum RoutingType
    {
        RawCode,
        HybridGraph
    }

    public class PythonAnalyzeResponse
    {
        public bool IsValid { get; set; }
        public int Sloc { get; set; }
        public int Vg { get; set; }
    }

    public class RouterDecisionDto
    {
        public bool IsValidSyntax { get; set; }
        public int Sloc { get; set; }
        public int Vg { get; set; }
        
        /// <summary>
        /// Quyết định định tuyến: sử dụng RawCode hay HybridGraph
        /// </summary>
        public RoutingType SelectedRoute { get; set; }
        
        /// <summary>
        /// Thông báo chi tiết (nếu có lỗi biên dịch / logic)
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
