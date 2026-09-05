namespace Repo_Into_Graph_Application.Dtos.HybridContextGenerator
{
    /// <summary>
    /// Input contract cho Tang 2 - Hybrid Context Generator.
    /// Duoc dong goi boi Orchestrator sau khi Tang 1 quyet dinh ROUTE_HYBRID.
    /// </summary>
    public class HybridContextInputDto
    {
        /// <summary>
        /// ID dinh danh module/ham duoc phan tich. Do client cung cap.
        /// Vd: "MOD_001", "AppointmentService.createAppointment"
        /// </summary>
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>
        /// Ngon ngu lap trinh: "java" hoac "csharp"
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Quyet dinh dinh tuyen tu Tang 1: "ROUTE_HYBRID" hoac "ROUTE_RAW_CODE"
        /// </summary>
        public string RoutingDecision { get; set; } = "ROUTE_HYBRID";

        /// <summary>
        /// Cac chi so do luong duoc tinh boi Tang 1
        /// </summary>
        public HybridContextMetricsDto Metrics { get; set; } = new();

        /// <summary>
        /// Ma nguon goc (raw source code) cua ham/class can phan tich
        /// </summary>
        public string RawSourceCode { get; set; } = string.Empty;

        /// <summary>
        /// Metadata cua AST tu Python Tree-sitter (Tang 1)
        /// </summary>
        public AstPayloadDto AstPayload { get; set; } = new();
    }

    public class HybridContextMetricsDto
    {
        public int Sloc { get; set; }
        public int CyclomaticComplexity { get; set; }
    }

    public class AstPayloadDto
    {
        public string ParserType { get; set; } = "tree-sitter";
        public string RootNodeType { get; set; } = string.Empty;
        public bool HasError { get; set; }
    }

    /// <summary>
    /// Output placeholder tu Tang 2 Stub.
    /// Se duoc mo rong khi implement Tang 2 day du.
    /// </summary>
    public class HybridContextOutputDto
    {
        public string ModuleId { get; set; } = string.Empty;
        public string Status { get; set; } = "PENDING";
        public string Message { get; set; } = string.Empty;
    }
}
