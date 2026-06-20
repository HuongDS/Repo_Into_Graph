using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_Application.Dtos.Feature
{
    // ─── Step DTO ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thông tin một bước trong feature (luồng phân tích).
    /// </summary>
    public class FeatureStepDto
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
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
    /// Thông tin tóm tắt của một Feature dùng trong danh sách (không kèm steps).
    /// </summary>
    public class FeatureSummaryDto
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
    /// Thông tin đầy đủ của một Feature kèm steps và mermaid graphs.
    /// Frontend có thể dùng MermaidGraph / DataFlowMermaidGraph để render trực tiếp.
    /// </summary>
    public class FeatureDetailDto
    {
        public Guid Id { get; set; }
        public Guid AnalysisRunId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // ── Steps ─────────────────────────────────────────────────────────────
        public List<FeatureStepDto> Steps { get; set; } = new();

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
        /// Tổng số bước trong feature.
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
    /// Kết quả phân trang cho FeatureSummaryDto.
    /// </summary>
    public class FeaturePagedResult
    {
        public IEnumerable<FeatureSummaryDto> Items { get; set; }
            = System.Linq.Enumerable.Empty<FeatureSummaryDto>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
