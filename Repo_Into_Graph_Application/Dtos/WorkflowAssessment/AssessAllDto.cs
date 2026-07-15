namespace Repo_Into_Graph_Application.Dtos.WorkflowAssessment
{
    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Input cho POST /api/WorkflowAssessment/assess-all
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Body JSON cho endpoint <c>POST /api/WorkflowAssessment/assess-all</c>.
    ///
    /// <para>
    /// Endpoint tổng hợp (Orchestrator) thực thi toàn bộ Pipeline đánh giá câu hỏi:
    /// <list type="number">
    ///   <item>Bước 1–2: Semantic Mapping + Path Matching (assess-accuracy)</item>
    ///   <item>Bước 3: Metrics Calculation – Độ Khó (assess-difficulty)</item>
    /// </list>
    /// Trả về <see cref="AssessAllResultDto"/> tổng hợp cả hai kết quả.
    /// </para>
    /// </summary>
    public class AssessAllRequestDto
    {
        /// <summary>Câu hỏi nghiệp vụ ngôn ngữ tự nhiên cần đánh giá toàn diện.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// Dữ liệu đồ thị luồng của Use Case / Workflow liên quan.
        /// Lấy trực tiếp từ <see cref="Repo_Into_Graph_Application.Dtos.QuestionGenerate.GenerateQuestionsResponse"/>
        /// (nodes + edges của workflow).
        /// </summary>
        public WorkflowDataDto WorkflowData { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Output tổng hợp của POST /api/WorkflowAssessment/assess-all
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả tổng hợp sau khi Pipeline đánh giá toàn diện hoàn thành.
    /// Bao gồm cả kết quả Tính Chính xác (assess-accuracy) và Độ Khó (assess-difficulty).
    /// </summary>
    public class AssessAllResultDto
    {
        /// <summary>Câu hỏi gốc được đánh giá.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>Tên Workflow được dùng để đánh giá.</summary>
        public string WorkflowName { get; set; } = string.Empty;

        // ── Kết quả Bước 1 + 2: Tính Chính xác ──────────────────────────────

        /// <summary>
        /// Kết quả đánh giá tính chính xác (Accuracy):
        /// chuỗi G_q được trích xuất, liên thông hay đứt gãy, FinalVerdict.
        /// </summary>
        public AccuracyAssessmentResultDto AccuracyResult { get; set; } = new();

        // ── Kết quả Bước 3: Độ Khó ──────────────────────────────────────────

        /// <summary>
        /// Kết quả đánh giá độ khó (Difficulty):
        /// V(G), L_q, GatewaysCount, PathType, Level, Reasoning.
        /// </summary>
        public DifficultyAssessmentResultDto DifficultyResult { get; set; } = new();

        // ── Metadata tổng hợp ────────────────────────────────────────────────

        /// <summary>Tổng số nút Active được kích hoạt bởi câu hỏi.</summary>
        public int ActiveNodeCount { get; set; }

        /// <summary>Tổng số nút trong Workflow.</summary>
        public int WorkflowNodeCount { get; set; }

        /// <summary>Tổng số cạnh trong Workflow.</summary>
        public int WorkflowEdgeCount { get; set; }
    }
}
