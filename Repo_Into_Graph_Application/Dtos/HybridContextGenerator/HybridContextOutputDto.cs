using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Repo_Into_Graph_Application.Dtos.HybridContextGenerator
{
    /// <summary>
    /// Output contract cua Tang 2 - Hybrid Context Generator.
    ///
    /// 4 truong dau tien la CONTRACT CHINH (snake_case theo dung dac ta):
    ///   status | route_decision | hybrid_prompt | metrics
    /// Cac truong con lai la du lieu chi tiet de Tang 3 va tool test dung lai.
    /// </summary>
    public class HybridContextOutputDto
    {
        /// <summary>SUCCESS | PARTIAL | FAILED | PENDING</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "PENDING";

        /// <summary>Quyet dinh dinh tuyen nhan tu Tang 1: ROUTE_HYBRID | ROUTE_RAW_CODE</summary>
        [JsonPropertyName("route_decision")]
        public string RouteDecision { get; set; } = string.Empty;

        /// <summary>
        /// Ngu canh lai hoan chinh (CFG skeleton + critical decision logic + enriched metadata)
        /// - nap thang vao prompt cua Tang 3.
        /// </summary>
        [JsonPropertyName("hybrid_prompt")]
        public string HybridPrompt { get; set; } = string.Empty;

        /// <summary>Chi so do luong cua module va muc tiet kiem token.</summary>
        [JsonPropertyName("metrics")]
        public HybridMetricsDto Metrics { get; set; } = new();

        // --- DU LIEU CHI TIET (bo sung) ---

        /// <summary>ID module duoc phan tich (tool test dung de doi chieu).</summary>
        [JsonPropertyName("moduleId")]
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>Ngon ngu duoc su dung khi phan tich (java | csharp).</summary>
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Bo phan tich da dung: "tree-sitter" (qua Python Microservice) hoac
        /// "builtin" (parser noi bo cua .NET khi service khong san sang).
        /// </summary>
        [JsonPropertyName("parser")]
        public string Parser { get; set; } = "builtin";

        /// <summary>Thong diep mo ta ket qua xu ly.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>Rieng phan khung CFG dang Mermaid (da nam trong hybrid_prompt).</summary>
        [JsonPropertyName("cfgSkeleton")]
        public string CfgSkeleton { get; set; } = string.Empty;

        /// <summary>Cac doan code quan trong duoc trich nguyen van tu source.</summary>
        [JsonPropertyName("criticalSnippets")]
        public List<string> CriticalSnippets { get; set; } = new();

        /// <summary>Ban day du cua Critical Snippets kem node CFG, ly do va vi tri dong.</summary>
        [JsonPropertyName("criticalSnippetDetails")]
        public List<CriticalSnippetDto> CriticalSnippetDetails { get; set; } = new();

        /// <summary>Metadata lam giau: async markers, annotation tags, dependency info...</summary>
        [JsonPropertyName("enrichedMetadata")]
        public EnrichedMetadataDto EnrichedMetadata { get; set; } = new();

        /// <summary>Danh sach node cua CFG (de Tang 3 xu ly bang code thay vi parse Mermaid).</summary>
        [JsonPropertyName("cfgNodes")]
        public List<CfgNodeDto> CfgNodes { get; set; } = new();

        /// <summary>Danh sach canh (edge) cua CFG.</summary>
        [JsonPropertyName("cfgEdges")]
        public List<CfgEdgeDto> CfgEdges { get; set; } = new();

        /// <summary>Cac canh bao khong lam hong ket qua (vd: AST co loi cu phap).</summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();

        /// <summary>Thoi gian xu ly (ms).</summary>
        [JsonPropertyName("processing_time_ms")]
        public long ProcessingTimeMs { get; set; }

        /// <summary>Thoi diem sinh ket qua (UTC).</summary>
        [JsonPropertyName("generated_at_utc")]
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Chi so do luong tra ve kem ngu canh lai.</summary>
    public class HybridMetricsDto
    {
        /// <summary>SLOC goc do Tang 1 tinh.</summary>
        [JsonPropertyName("original_sloc")]
        public int OriginalSloc { get; set; }

        /// <summary>V(G) do Tang 1 tinh.</summary>
        [JsonPropertyName("cyclomatic_complexity")]
        public int CyclomaticComplexity { get; set; }

        /// <summary>
        /// % token tiet kiem duoc khi dung hybrid_prompt thay cho raw source code:
        /// (1 - token(hybrid_prompt) / token(raw_source)) * 100.
        /// Gia tri am = ngu canh lai dai hon ma nguon goc (ham qua ngan - dang le di ROUTE_RAW_CODE).
        /// </summary>
        [JsonPropertyName("estimated_token_saving_pct")]
        public double EstimatedTokenSavingPct { get; set; }

        /// <summary>V(G) tinh lai tu CFG: E - N + 2P.</summary>
        [JsonPropertyName("cyclomatic_complexity_cfg")]
        public int CyclomaticComplexityFromCfg { get; set; }

        [JsonPropertyName("original_tokens")]
        public int OriginalTokens { get; set; }

        [JsonPropertyName("hybrid_tokens")]
        public int HybridTokens { get; set; }

        [JsonPropertyName("cfg_node_count")]
        public int CfgNodeCount { get; set; }

        [JsonPropertyName("cfg_edge_count")]
        public int CfgEdgeCount { get; set; }

        [JsonPropertyName("method_count")]
        public int MethodCount { get; set; }

        [JsonPropertyName("critical_snippet_count")]
        public int CriticalSnippetCount { get; set; }
    }

    /// <summary>Mot node trong Control Flow Graph.</summary>
    public class CfgNodeDto
    {
        /// <summary>Dinh danh node tren so do Mermaid: A, B, C, ... Z, AA, AB...</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Nhan hien thi tren so do (vd: "Check count >= MAX").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>START | END | PROCESS | DECISION | THROW | RETURN | CATCH | FINALLY</summary>
        public string Kind { get; set; } = "PROCESS";

        /// <summary>Dong bat dau trong raw source (1-based). 0 neu la node tong hop.</summary>
        public int Line { get; set; }

        /// <summary>Doan code goc tuong ung voi node (de truy vet ve source).</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Ten ham chua node nay.</summary>
        public string Method { get; set; } = string.Empty;
    }

    /// <summary>Mot canh trong Control Flow Graph.</summary>
    public class CfgEdgeDto
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;

        /// <summary>Nhan tren canh: Yes / No / loop / exception / case x ... (co the rong).</summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>Mot doan code quan trong kem ly do duoc chon.</summary>
    public class CriticalSnippetDto
    {
        public string Code { get; set; } = string.Empty;

        /// <summary>Node CFG tuong ung (vd: "B") - dung de tham chieu trong hybrid_prompt.</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>BRANCH | THROW | CATCH | LOOP | SWITCH | ASYNC | DEPENDENCY_CALL | RETURN</summary>
        public string Kind { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public int Line { get; set; }

        /// <summary>Do uu tien (cang cao cang quan trong).</summary>
        public int Weight { get; set; }

        public string Method { get; set; } = string.Empty;
    }

    /// <summary>Metadata lam giau cho ngu canh lai.</summary>
    public class EnrichedMetadataDto
    {
        public bool IsAsync { get; set; }

        public List<AsyncMarkerDto> AsyncMarkers { get; set; } = new();

        public List<string> AnnotationTags { get; set; } = new();

        public List<DependencyDto> Dependencies { get; set; } = new();

        public List<string> ThrownExceptions { get; set; } = new();

        public List<string> CaughtExceptions { get; set; } = new();

        public List<MethodSignatureDto> Methods { get; set; } = new();

        public ControlFlowStatsDto ControlFlow { get; set; } = new();

        /// <summary>Tag ngu nghia: async, has-loop, throws-exception, cache, database...</summary>
        public List<string> Tags { get; set; } = new();
    }

    public class AsyncMarkerDto
    {
        /// <summary>ASYNC_MODIFIER | AWAIT | ASYNC_CALL | TASK | FUTURE | ANNOTATION | REACTIVE</summary>
        public string Marker { get; set; } = string.Empty;
        public int Line { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class DependencyDto
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>FIELD_CALL | STATIC_CALL | INSTANTIATION | PARAMETER</summary>
        public string Kind { get; set; } = string.Empty;

        public int UsageCount { get; set; }

        public List<string> Members { get; set; } = new();
    }

    public class MethodSignatureDto
    {
        public string Name { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
        public List<string> Modifiers { get; set; } = new();
        public List<string> Annotations { get; set; } = new();
        public bool IsAsync { get; set; }
        public bool IsStatic { get; set; }
        public int StartLine { get; set; }
    }

    public class ControlFlowStatsDto
    {
        public int BranchCount { get; set; }
        public int LoopCount { get; set; }
        public int SwitchCount { get; set; }
        public int TryCatchCount { get; set; }
        public int ThrowCount { get; set; }
        public int ReturnCount { get; set; }
        public int MaxNestingDepth { get; set; }
        public bool HasComplexLoop { get; set; }
    }
}
