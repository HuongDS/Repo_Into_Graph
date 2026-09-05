using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

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
        public string RootNodeType { get; set; } = string.Empty;
        public bool HasError { get; set; }
    }

    public class RouterDecisionDto
    {
        public bool IsValidSyntax { get; set; }
        public int Sloc { get; set; }
        public int Vg { get; set; }
        
        /// <summary>
        /// Quyet dinh dinh tuyen: su dung RawCode hay HybridGraph
        /// </summary>
        public RoutingType SelectedRoute { get; set; }
        
        /// <summary>
        /// Thong bao chi tiet (neu co loi bien dich / logic)
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Ket qua tu Tang 2 - chi co gia tri khi SelectedRoute == HybridGraph.
        /// Se la null neu SelectedRoute == RawCode.
        /// </summary>
        public HybridContextOutputDto? HybridContextResult { get; set; }
    }
}
