namespace Repo_Into_Graph_Application.Dtos.WorkflowAssessment
{
    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Nút đồ thị truyền vào cho assess-difficulty
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đại diện một nút Active trong đồ thị con G_q đã được xác thực ở bước assess-accuracy.
    /// Trường <see cref="NodeType"/> xác định loại nút để đếm DecisionGateway.
    /// </summary>
    public class GraphNodeDto
    {
        /// <summary>Định danh duy nhất của nút (khớp với NodeId từ bước assess-accuracy).</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Tên hiển thị của bước nghiệp vụ.</summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// Loại nút: "Activity" (mặc định) hoặc "DecisionGateway" (nút rẽ nhánh).
        /// Giá trị hợp lệ khớp với enum <see cref="NodeType"/> nhưng được truyền dưới dạng string
        /// để dễ serialize/deserialize qua HTTP.
        /// </summary>
        public string NodeType { get; set; } = "Activity";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Input cho POST /api/WorkflowAssessment/assess-difficulty
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Body JSON cho endpoint <c>POST /api/WorkflowAssessment/assess-difficulty</c>.
    ///
    /// <para>
    /// Nhận vào danh sách Active Nodes đã được xác thực (từ kết quả bước assess-accuracy)
    /// cùng kích thước đồ thị con G_q để tính Cyclomatic Complexity V(G) = E_q - V_q + 2.
    /// </para>
    /// </summary>
    public class DifficultyAssessmentRequestDto
    {
        /// <summary>
        /// Danh sách các nút Active đã được xác thực tính liên thông từ bước assess-accuracy.
        /// Thứ tự các nút trong danh sách này chính là thứ tự dịch chuyển logic của câu hỏi.
        /// </summary>
        public List<GraphNodeDto> ActiveNodes { get; set; } = new();

        /// <summary>
        /// Tổng số Cạnh (E_q) của đồ thị con cảm sinh G_q — CHỈ đếm những cạnh có
        /// CẢ HAI đầu mút nằm trong tập ActiveNodes. Dùng để tính V(G) = E_q - V_q + 2P.
        ///
        /// <para>
        /// LƯU Ý: tuyệt đối KHÔNG truyền giá trị suy ra từ số nút (ví dụ ActiveNodes.Count - 1).
        /// Làm vậy sẽ ép V(G) = 1 với mọi dữ liệu đầu vào (đẳng thức đại số), khiến chỉ số
        /// mất hoàn toàn phương sai và không còn đo lường được gì.
        /// </para>
        /// </summary>
        public int TotalEdgesInSubgraph { get; set; }

        /// <summary>
        /// Số thành phần liên thông (P) của đồ thị con G_q, dùng cho công thức McCabe đầy đủ
        /// V(G) = E_q - V_q + 2P. Với đồ thị con liên thông thì P = 1.
        /// Mặc định 1 để tương thích ngược với các lời gọi cũ.
        /// </summary>
        public int ConnectedComponents { get; set; } = 1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Output của POST /api/WorkflowAssessment/assess-difficulty
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả đánh giá Độ Khó dựa trên 3 chỉ số đồ thị:
    /// V(G) – Cyclomatic Complexity, L_q – Impact Path Length, PathType.
    /// </summary>
    public class DifficultyAssessmentResultDto
    {
        /// <summary>Mức độ phân loại cuối cùng: "Dễ" | "Trung bình" | "Khó".</summary>
        public string Level { get; set; } = string.Empty;

        /// <summary>
        /// Độ phức tạp tuần hoàn: V(G) = E_q - V_q + 2.
        /// Đo lường số kịch bản rẽ nhánh độc lập trong câu hỏi.
        /// </summary>
        public int CyclomaticComplexity { get; set; }

        /// <summary>
        /// Độ sâu chuỗi tác động: L_q = ActiveNodes.Count - 1.
        /// Đo lường số bước dịch chuyển logic liên tiếp.
        /// </summary>
        public int ImpactPathLength { get; set; }

        /// <summary>Tổng số nút DecisionGateway trong chuỗi Active Nodes.</summary>
        public int GatewaysCount { get; set; }

        /// <summary>
        /// Loại nhánh kích hoạt:
        /// "Happy Path" (0 gateway) | "Single Exception" (1 gateway) | "Double Exception" (≥2 gateways).
        /// </summary>
        public string PathType { get; set; } = string.Empty;

        /// <summary>
        /// Lời chứng minh định lượng: giải thích tại sao câu hỏi đạt mức độ khó đó,
        /// bao gồm các con số cụ thể V(G), L_q, GatewaysCount.
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Kết quả Đánh giá Độ Khó Hàng Loạt (Batch)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kết quả đánh giá độ khó cho một danh sách câu hỏi.
    /// </summary>
    public class BatchDifficultyAssessmentResultDto
    {
        public Guid BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public List<QuestionDifficultyAssessmentResultDto> QuestionResults { get; set; } = new();
    }

    /// <summary>
    /// Kết quả đánh giá độ khó của một câu hỏi riêng lẻ trong danh sách.
    /// </summary>
    public class QuestionDifficultyAssessmentResultDto
    {
        public string Question { get; set; } = string.Empty;
        public DifficultyAssessmentResultDto DifficultyResult { get; set; } = new();
    }
}
