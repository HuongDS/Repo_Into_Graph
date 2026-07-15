namespace Repo_Into_Graph_Application.Dtos.WorkflowAssessment
{
    // ─────────────────────────────────────────────────────────────────────────
    // ENUM: Loại nút trong đồ thị luồng
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Phân loại nút trong đồ thị luồng nghiệp vụ.
    /// DecisionGateway → nút rẽ nhánh (if/switch) → ảnh hưởng đến Độ Khó.
    /// </summary>
    public enum NodeType
    {
        /// <summary>Bước xử lý thông thường (action, task, …).</summary>
        Activity,

        /// <summary>Điểm bắt đầu của luồng.</summary>
        StartEvent,

        /// <summary>Điểm kết thúc của luồng.</summary>
        EndEvent,

        /// <summary>Nút rẽ nhánh điều kiện – dùng để tính Độ Khó.</summary>
        DecisionGateway,

        /// <summary>Nút hội tụ nhiều nhánh lại.</summary>
        MergeGateway,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Nút (Node)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Biểu diễn một đỉnh trong đồ thị luồng.
    /// </summary>
    public class NodeDto
    {
        /// <summary>Định danh duy nhất của nút (ví dụ: GUID hoặc chuỗi có nghĩa).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Tên hiển thị của bước nghiệp vụ.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Phân loại nút – quyết định cách tính Độ Khó.</summary>
        public NodeType Type { get; set; } = NodeType.Activity;

        /// <summary>
        /// Mô tả ngữ nghĩa của bước này, dùng để so khớp với Câu hỏi ở Bước 1.
        /// Nên viết bằng ngôn ngữ tự nhiên, rõ ràng, đủ từ khóa.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách từ khóa bổ sung giúp Semantic Matching chính xác hơn.
        /// </summary>
        public List<string> Keywords { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Cạnh (Edge)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Biểu diễn một cạnh có hướng trong đồ thị luồng.
    /// </summary>
    public class EdgeDto
    {
        /// <summary>Node ID của đầu nguồn (nơi xuất phát).</summary>
        public string FromNodeId { get; set; } = string.Empty;

        /// <summary>Node ID của đầu đích (nơi đến).</summary>
        public string ToNodeId { get; set; } = string.Empty;

        /// <summary>Nhãn điều kiện của cạnh (ví dụ: "Yes", "No", "Error").</summary>
        public string? Label { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Luồng được chọn (SelectedWorkflow)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đồ thị của một Use Case / Workflow cụ thể mà Câu hỏi cần được đánh giá trong ngữ cảnh đó.
    /// </summary>
    public class WorkflowGraphDto
    {
        /// <summary>Định danh duy nhất của Workflow.</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>Tên mô tả của Workflow (ví dụ: "Đăng ký tài khoản", "Thanh toán đơn hàng").</summary>
        public string WorkflowName { get; set; } = string.Empty;

        /// <summary>Tập hợp các nút thuộc Workflow này.</summary>
        public List<NodeDto> Nodes { get; set; } = new();

        /// <summary>Tập hợp các cạnh có hướng của Workflow này.</summary>
        public List<EdgeDto> Edges { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Đồ thị toàn cục (GlobalGraph)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đồ thị tổng hợp toàn bộ hệ thống – dùng để tính tỉ lệ bao phủ bắc cầu.
    /// Chứa tất cả các nút của mọi Workflow trong hệ thống.
    /// </summary>
    public class GlobalGraphDto
    {
        /// <summary>Toàn bộ nút của hệ thống (gộp từ mọi Workflow).</summary>
        public List<NodeDto> AllNodes { get; set; } = new();

        /// <summary>Toàn bộ cạnh của hệ thống.</summary>
        public List<EdgeDto> AllEdges { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Đầu vào tổng hợp cho Service
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đóng gói toàn bộ dữ liệu đầu vào cần thiết cho WorkflowAssessmentService.
    /// </summary>
    public class AssessmentRequestDto
    {
        /// <summary>Câu hỏi nghiệp vụ ngôn ngữ tự nhiên cần đánh giá.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>Luồng Use Case được chọn để đánh giá Câu hỏi trong ngữ cảnh đó.</summary>
        public WorkflowGraphDto SelectedWorkflow { get; set; } = new();

        /// <summary>Đồ thị toàn cục của hệ thống.</summary>
        public GlobalGraphDto GlobalGraph { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Kết quả đánh giá (Assessment Result)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả trả về sau khi Pipeline 3 bước hoàn thành.
    /// </summary>
    public class AssessmentResultDto
    {
        // ── Bước 1: Active Nodes ──────────────────────────────────────────────

        /// <summary>Danh sách Node ID được kích hoạt theo thứ tự xuất hiện ngữ nghĩa trong Câu hỏi.</summary>
        public List<string> ActiveNodeIds { get; set; } = new();

        /// <summary>Chi tiết của các nút Active (để hiển thị ra UI).</summary>
        public List<NodeDto> ActiveNodes { get; set; } = new();

        // ── Bước 2: Path Accuracy ─────────────────────────────────────────────

        /// <summary>
        /// true  → chuỗi Active Nodes tạo thành một đường đi liên tục hợp lệ trong Workflow.
        /// false → có đứt gãy luồng (missing edge).
        /// </summary>
        public bool IsAccurate { get; set; }

        /// <summary>Danh sách các cặp nút bị đứt gãy (FromId → ToId) nếu IsAccurate = false.</summary>
        public List<(string FromId, string ToId)> BrokenLinks { get; set; } = new();

        // ── Bước 3: Metrics ───────────────────────────────────────────────────

        /// <summary>Tỉ lệ nút Workflow / nút toàn cục (= Coverage_Luong_Graph).</summary>
        public double CoverageWorkflowOverGlobal { get; set; }

        /// <summary>Tỉ lệ nút Active / nút Workflow (= Coverage_CauHoi_Luong).</summary>
        public double CoverageActiveOverWorkflow { get; set; }

        /// <summary>Độ bao phủ tổng (bắc cầu) = CoverageWorkflowOverGlobal × CoverageActiveOverWorkflow.</summary>
        public double TotalCoverage { get; set; }

        /// <summary>Độ sâu chuỗi = Số nút Active - 1.</summary>
        public int ImpactPathLength { get; set; }

        /// <summary>Số nút loại DecisionGateway trong chuỗi Active Nodes.</summary>
        public int GatewaysCount { get; set; }

        /// <summary>Phân loại độ khó: "Dễ" | "Trung bình" | "Khó".</summary>
        public string DifficultyLevel { get; set; } = string.Empty;

        /// <summary>
        /// Số cạnh E_q của đồ thị con G_q (chỉ tính cạnh kết nối giữa các Active Nodes).
        /// Dùng để tính V(G) = E_q - V_q + 2 ở tầng Controller/DifficultyAssessmentService.
        /// </summary>
        public int SubgraphEdgeCount { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Kết quả đánh giá của MỘT câu hỏi (dùng trong Batch)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đóng gói câu hỏi gốc cùng toàn bộ kết quả Pipeline 3 bước của câu hỏi đó.
    /// </summary>
    public class QuestionAssessmentResultDto
    {
        /// <summary>Câu hỏi nghiệp vụ ngôn ngữ tự nhiên gốc.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>Gợi ý đáp án từ hệ thống sinh câu hỏi.</summary>
        public string SuggestedAnswer { get; set; } = string.Empty;

        /// <summary>Độ khó AI tự sinh (label gốc từ QuestionGenerate).</summary>
        public string AiDifficulty { get; set; } = string.Empty;

        /// <summary>Kết quả Pipeline 3 bước sau khi đánh giá trên đồ thị.</summary>
        public AssessmentResultDto? GraphAssessment { get; set; }

        /// <summary>Độ bao phủ tổng (bắc cầu) của câu hỏi này.</summary>
        public double Coverage { get; set; }

        /// <summary>Tỉ lệ nút Workflow / nút toàn cục của câu hỏi này.</summary>
        public double CoverageWorkflowOverGlobal { get; set; }

        /// <summary>Tỉ lệ nút Active / nút Workflow của câu hỏi này.</summary>
        public double CoverageActiveOverWorkflow { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Kết quả Batch đánh giá toàn bộ GenerateQuestionsResponse
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả tổng hợp khi đánh giá toàn bộ danh sách câu hỏi của một Business.
    /// </summary>
    public class BatchAssessmentResultDto
    {
        /// <summary>ID Business được đánh giá.</summary>
        public Guid BusinessId { get; set; }

        /// <summary>Tên Business.</summary>
        public string BusinessName { get; set; } = string.Empty;

        /// <summary>Số lượng câu hỏi được đánh giá.</summary>
        public int TotalQuestions { get; set; }

        /// <summary>Số câu hỏi có luồng chính xác (IsAccurate = true).</summary>
        public int AccurateCount { get; set; }

        /// <summary>Phần trăm câu hỏi chính xác (AccurateCount / TotalQuestions).</summary>
        public double AccuracyRate { get; set; }

        /// <summary>Coverage trung bình của toàn bộ câu hỏi.</summary>
        public double AverageTotalCoverage { get; set; }

        /// <summary>Số nút trong đồ thị Workflow của Business (SelectedWorkflow).</summary>
        public int WorkflowNodeCount { get; set; }

        /// <summary>Số nút trong đồ thị toàn cục (GlobalGraph).</summary>
        public int GlobalNodeCount { get; set; }

        /// <summary>Kết quả chi tiết từng câu hỏi.</summary>
        public List<QuestionAssessmentResultDto> QuestionResults { get; set; } = new();
    }
}
