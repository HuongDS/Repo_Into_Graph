using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Repo_Into_Graph.Repo_Into_Graph.Dtos.BusinessFlow
{
    // ─── Step DTO ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thông tin một bước trong business flow.
    /// </summary>
    public class BusinessFlowStepDto
    {
        public Guid Id { get; set; }
        public Guid BusinessFlowId { get; set; }
        public int StepOrder { get; set; }
        public string CallerClass { get; set; } = string.Empty;
        public string CallerMethod { get; set; } = string.Empty;
        public string CalleeClass { get; set; } = string.Empty;
        public string CalleeMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // ── Derived / convenience ──────────────────────────────────────────────
        /// <summary>
        /// Chuỗi hiển thị: "CallerClass.CallerMethod → CalleeClass.CalleeMethod"
        /// </summary>
        public string DisplayLabel =>
            $"{CallerClass}.{CallerMethod} → {CalleeClass}.{CalleeMethod}";
    }

    // ─── Summary DTO (list) ───────────────────────────────────────────────────────

    /// <summary>
    /// Thông tin tóm tắt của một BusinessFlow dùng trong danh sách (không kèm steps).
    /// </summary>
    public class BusinessFlowSummaryDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public int StepCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ─── Detail DTO (single) ──────────────────────────────────────────────────────

    /// <summary>
    /// Thông tin đầy đủ của một BusinessFlow kèm steps và mermaid graphs.
    /// Frontend có thể dùng MermaidGraph / DataFlowMermaidGraph để render trực tiếp.
    /// </summary>
    public class BusinessFlowDetailDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // ── Steps ─────────────────────────────────────────────────────────────
        public List<BusinessFlowStepDto> Steps { get; set; } = new();

        // ── Mermaid graphs ────────────────────────────────────────────────────
        /// <summary>
        /// Mermaid sequence diagram của call-flow (dùng để render ở frontend).
        /// </summary>
        public string MermaidGraph { get; set; } = string.Empty;

        /// <summary>
        /// Mermaid graph của data-flow (dùng để render ở frontend).
        /// </summary>
        public string DataFlowMermaidGraph { get; set; } = string.Empty;

        // ── Computed helpers for frontend ─────────────────────────────────────
        /// <summary>
        /// Tổng số bước trong flow.
        /// </summary>
        public int StepCount => Steps.Count;

        /// <summary>
        /// Cho biết mermaid call-flow graph có dữ liệu hay không.
        /// </summary>
        public bool HasMermaidGraph => !string.IsNullOrWhiteSpace(MermaidGraph);

        /// <summary>
        /// Cho biết mermaid data-flow graph có dữ liệu hay không.
        /// </summary>
        public bool HasDataFlowGraph => !string.IsNullOrWhiteSpace(DataFlowMermaidGraph);
    }

    // ─── Paged result ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả phân trang cho BusinessFlowSummaryDto.
    /// </summary>
    public class BusinessFlowPagedResult
    {
        public IEnumerable<BusinessFlowSummaryDto> Items { get; set; }
            = System.Linq.Enumerable.Empty<BusinessFlowSummaryDto>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
