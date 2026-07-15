using System.Text.Json.Serialization;

namespace Repo_Into_Graph_Application.Dtos.WorkflowAssessment
{
    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Response tổng hợp 3 bước (Comprehensive Pipeline)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Response tổng hợp của endpoint <c>POST /api/WorkflowAssessment/assess-from-response</c>
    /// sau khi được nâng cấp với Pipeline 3 bước đầy đủ.
    ///
    /// <list type="number">
    ///   <item>Bước 1 – Coverage: Tỉ lệ bao phủ đồ thị (từ AssessFromResponseAsync)</item>
    ///   <item>Bước 2 – Accuracy: Kiểm tra tính chính xác luồng từng câu hỏi</item>
    ///   <item>Bước 3 – Difficulty: Đo lường độ khó bằng chỉ số đồ thị (chỉ khi Bước 2 = true)</item>
    /// </list>
    ///
    /// <para>
    /// <b>Short-Circuit Logic:</b> Nếu bất kỳ câu hỏi nào có <c>IsAccurate = false</c>,
    /// trường <c>Difficulty</c> của câu hỏi đó sẽ là <c>null</c>.
    /// </para>
    /// </summary>
    public class ComprehensiveAssessmentResponse
    {
        /// <summary>
        /// true  → toàn bộ câu hỏi đều có luồng chính xác.<br/>
        /// false → có ít nhất một câu hỏi bị đứt gãy luồng (short-circuit đã kích hoạt).
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>Thông điệp tổng hợp mô tả kết quả đánh giá.</summary>
        public string Message { get; set; } = string.Empty;

        // ── Bước 1: Coverage ─────────────────────────────────────────────────

        /// <summary>Kết quả Độ bao phủ tổng hợp (từ <c>AssessFromResponseAsync</c>).</summary>
        public ComprehensiveCoverageDto Coverage { get; set; } = new();

        // ── Bước 2 + 3: Per-Question Accuracy & Difficulty ───────────────────

        /// <summary>
        /// Danh sách kết quả chi tiết từng câu hỏi bao gồm:
        /// Accuracy (luồng liên thông/đứt gãy) + Difficulty (độ khó định lượng).
        /// </summary>
        public List<ComprehensiveQuestionResultDto> QuestionAssessments { get; set; } = new();

        // ── Thống kê nhanh ──────────────────────────────────────────────────

        /// <summary>Số câu hỏi có luồng chính xác (IsAccurate = true).</summary>
        public int TotalAccurate { get; set; }

        /// <summary>Số câu hỏi có luồng đứt gãy (IsAccurate = false).</summary>
        public int TotalInaccurate { get; set; }

        /// <summary>Phân phối độ khó dưới dạng chuỗi mô tả (ví dụ: "Dễ: 1, Trung bình: 2, Khó: 2").</summary>
        public string DifficultyDistribution { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Coverage tổng hợp (Bước 1)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả Độ bao phủ tổng hợp của toàn bộ bộ câu hỏi,
    /// mapping trực tiếp từ <see cref="BatchAssessmentResultDto"/>.
    /// </summary>
    public class ComprehensiveCoverageDto
    {
        /// <summary>ID Business được đánh giá.</summary>
        public Guid BusinessId { get; set; }

        /// <summary>Tên Business.</summary>
        public string BusinessName { get; set; } = string.Empty;

        /// <summary>Tổng số câu hỏi được đánh giá.</summary>
        public int TotalQuestions { get; set; }

        /// <summary>Số câu hỏi có luồng chính xác.</summary>
        public int AccurateCount { get; set; }

        /// <summary>Tỉ lệ câu hỏi chính xác (AccurateCount / TotalQuestions).</summary>
        public double AccuracyRate { get; set; }

        /// <summary>Độ bao phủ đồ thị trung bình trên toàn bộ câu hỏi.</summary>
        public double AverageTotalCoverage { get; set; }

        /// <summary>Số nút trong đồ thị Workflow của Business.</summary>
        public int WorkflowNodeCount { get; set; }

        /// <summary>Số nút trong đồ thị toàn cục (GlobalGraph).</summary>
        public int GlobalNodeCount { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Kết quả tổng hợp của MỘT câu hỏi (Bước 2 + 3)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đóng gói kết quả đầy đủ cho một câu hỏi: Accuracy + Difficulty.
    /// </summary>
    public class ComprehensiveQuestionResultDto
    {
        /// <summary>Câu hỏi nghiệp vụ gốc.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>Gợi ý đáp án từ hệ thống sinh câu hỏi.</summary>
        public string SuggestedAnswer { get; set; } = string.Empty;

        /// <summary>Nhãn độ khó do AI sinh gán (label gốc, chưa qua kiểm định đồ thị).</summary>
        public string AiDifficulty { get; set; } = string.Empty;

        // ── Bước 2: Accuracy ─────────────────────────────────────────────────

        /// <summary>
        /// Kết quả kiểm tra tính chính xác luồng (Bước 2 – Path Alignment).
        /// </summary>
        public ComprehensiveAccuracyDto Accuracy { get; set; } = new();

        // ── Bước 3: Difficulty ────────────────────────────────────────────────

        /// <summary>
        /// Kết quả đánh giá độ khó bằng chỉ số đồ thị (Bước 3 – Graph Metrics).
        /// <br/><b>null</b> nếu Bước 2 phát hiện luồng đứt gãy (short-circuit).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DifficultyAssessmentResultDto? Difficulty { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Accuracy chi tiết một câu hỏi (Bước 2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả kiểm tra tính chính xác luồng của một câu hỏi,
    /// mapping từ <see cref="AssessmentResultDto"/>.
    /// </summary>
    public class ComprehensiveAccuracyDto
    {
        /// <summary>
        /// true  → chuỗi Active Nodes tạo thành đường đi liên tục hợp lệ trong Workflow.<br/>
        /// false → có đứt gãy luồng (short-circuit kích hoạt, Difficulty = null).
        /// </summary>
        public bool IsAccurate { get; set; }

        /// <summary>Danh sách Node ID được kích hoạt (theo thứ tự ngữ nghĩa).</summary>
        public List<string> ActiveNodeIds { get; set; } = new();

        /// <summary>Chi tiết các nút Active (Id, Name, Type) để hiển thị ra UI.</summary>
        public List<ActiveNodeSummaryDto> ActiveNodes { get; set; } = new();

        /// <summary>Danh sách các cặp nút bị đứt gãy (chỉ có giá trị khi IsAccurate = false).</summary>
        public List<BrokenLinkDto> BrokenLinks { get; set; } = new();

        /// <summary>Độ bao phủ tổng (bắc cầu) = Coverage_Workflow × Coverage_CauHoi.</summary>
        public double TotalCoverage { get; set; }

        /// <summary>Tỉ lệ nút Workflow / nút toàn cục.</summary>
        public double CoverageWorkflowOverGlobal { get; set; }

        /// <summary>Tỉ lệ nút Active / nút Workflow.</summary>
        public double CoverageActiveOverWorkflow { get; set; }

        /// <summary>Số cạnh E_q của đồ thị con G_q (dùng để tính V(G)).</summary>
        public int SubgraphEdgeCount { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Tóm tắt một nút Active (dùng trong danh sách)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tóm tắt thông tin hiển thị của một nút Active trong chuỗi G_q.
    /// Giảm kích thước response bằng cách bỏ Description và Keywords dài.
    /// </summary>
    public class ActiveNodeSummaryDto
    {
        /// <summary>Định danh duy nhất của nút.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Tên hiển thị của bước nghiệp vụ.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Loại nút: "Activity" hoặc "DecisionGateway".</summary>
        public string Type { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Cặp nút đứt gãy (serialize-friendly, không dùng C# tuple)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mô tả một cặp nút bị đứt gãy luồng — phiên bản JSON-serializable
    /// thay thế cho kiểu tuple <c>(string FromId, string ToId)</c> trong
    /// <see cref="AssessmentResultDto.BrokenLinks"/>.
    /// </summary>
    public class BrokenLinkDto
    {
        /// <summary>Node ID của điểm xuất phát bị đứt gãy.</summary>
        public string FromId { get; set; } = string.Empty;

        /// <summary>Node ID của điểm đến bị đứt gãy.</summary>
        public string ToId { get; set; } = string.Empty;
    }
}
