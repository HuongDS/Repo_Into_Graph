using Repo_Into_Graph_Application.Dtos.QuestionGenerate;

namespace Repo_Into_Graph_Application.Dtos.WorkflowAssessment
{
    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Input Node – Nút nghiệp vụ được truyền vào từ WorkflowData
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mô tả một bước (nút) trong luồng nghiệp vụ, tương thích với cấu trúc
    /// dữ liệu được sinh ra bởi QuestionGenerateService.
    /// </summary>
    public class WorkflowNodeInputDto
    {
        /// <summary>Định danh duy nhất của nút (ví dụ: "V_01", "N_login").</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Tên ngắn gọn của bước nghiệp vụ (ví dụ: "Xác thực người dùng").</summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả chi tiết ngữ nghĩa của bước này bằng ngôn ngữ tự nhiên.
        /// Đây là nguồn chính để Gemini Embedding so sánh với câu hỏi.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Mã nguồn của hàm/node tương ứng, dùng để gửi cho LLM tham khảo.
        /// </summary>
        public string SourceCode { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Input Edge – Cạnh/Chuyển tiếp giữa các nút
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Biểu diễn một cạnh có hướng (transition) trong luồng nghiệp vụ.
    /// </summary>
    public class WorkflowEdgeInputDto
    {
        /// <summary>Node ID nguồn (xuất phát).</summary>
        public string FromNodeId { get; set; } = string.Empty;

        /// <summary>Node ID đích (đến).</summary>
        public string ToNodeId { get; set; } = string.Empty;

        /// <summary>Điều kiện chuyển tiếp (tùy chọn, ví dụ: "Success", "Fail").</summary>
        public string? Condition { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: WorkflowData – Đồ thị luồng được truyền vào
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Container chứa toàn bộ cấu trúc đồ thị của một Use Case / Workflow.
    /// Tương thích trực tiếp với dữ liệu từ <see cref="GenerateQuestionsResponse"/>.
    /// </summary>
    public class WorkflowDataDto
    {
        /// <summary>Tên của Workflow / Use Case.</summary>
        public string WorkflowName { get; set; } = string.Empty;

        /// <summary>Danh sách các nút (bước) nghiệp vụ.</summary>
        public List<WorkflowNodeInputDto> Nodes { get; set; } = new();

        /// <summary>Danh sách các cạnh (liên kết chuyển tiếp) của luồng.</summary>
        public List<WorkflowEdgeInputDto> Edges { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Request – Đầu vào cho endpoint assess-accuracy
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Body JSON cho <c>POST /api/workflowassessment/assess-accuracy</c>.
    /// </summary>
    public class AccuracyAssessmentRequestDto
    {
        /// <summary>Câu hỏi nghiệp vụ ngôn ngữ tự nhiên cần đánh giá tính chính xác.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// Dữ liệu đồ thị luồng của Use Case liên quan.
        /// Lấy trực tiếp từ <see cref="GenerateQuestionsResponse"/> (nodes + edges).
        /// Lưu ý: trường <c>CodeCoverage</c> trong GenerateQuestionsResponse bị bỏ qua
        /// vì chỉ số đó không chính xác.
        /// </summary>
        public WorkflowDataDto WorkflowData { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: ExtractedPathStep – Một bước trong chuỗi G_q được trich xuất
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Một phần tử trong chuỗi đường đi được trích xuất từ Câu hỏi
    /// sau khi thực hiện Semantic-to-Node Mapping (Bước 1).
    /// </summary>
    public class ExtractedPathStepDto
    {
        /// <summary>Thứ tự bước trong chuỗi (bắt đầu từ 1).</summary>
        public int Step { get; set; }

        /// <summary>Node ID chuẩn trong hệ thống.</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Tên nút nghiệp vụ.</summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// Cụm từ ngữ nghĩa được bóc tách từ Câu hỏi mà ánh xạ sang nút này.
        /// </summary>
        public string MatchedPhrase { get; set; } = string.Empty;

        /// <summary>Điểm tương đồng cosine [0.0 – 1.0] từ Gemini Embedding.</summary>
        public double SimilarityScore { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: BrokenTransition – Đứt gãy luồng (Bước 2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mô tả một cặp nút bị đứt gãy (không có cạnh nối) trong Bước 2.
    /// </summary>
    public class BrokenTransitionDto
    {
        /// <summary>Tên nút xuất phát.</summary>
        public string FromNode { get; set; } = string.Empty;

        /// <summary>Tên nút đến.</summary>
        public string ToNode { get; set; } = string.Empty;

        /// <summary>Lý do cụ thể tại sao chuyển tiếp này bị từ chối.</summary>
        public string Reason { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO: Result – Kết quả trả về của assess-accuracy
    // ─────────────────────────────────────────────────────────────────────────

    public class RubricScoresDto
    {
        public int Correctness { get; set; }
        public int Faithfulness { get; set; }
        public int ContextRelevance { get; set; }
        public int Clarity { get; set; }
        public int Answerability { get; set; }
    }

    /// <summary>
    /// Kết quả đầy đủ của Pipeline 2 bước đánh giá tính chính xác.
    /// </summary>
    public class AccuracyAssessmentResultDto
    {
        /// <summary>
        /// true  → toàn bộ chuỗi nút được trích xuất liên thông không đứt gãy.
        /// false → có ít nhất một cặp nút không tồn tại cạnh nối.
        /// </summary>
        public bool IsAccurate { get; set; }

        /// <summary>
        /// Điểm số chính xác (0.0 đến 1.0) tính dựa trên tỷ lệ bước chuyển tiếp đúng.
        /// Công thức: (Tổng số bước - Số bước gãy) / Tổng số bước.
        /// (Giữ lại để tương thích ngược)
        /// </summary>
        public double AccuracyScore { get; set; }

        /// <summary>
        /// Điểm số đánh giá chi tiết theo Rubric (Correctness, Faithfulness, ContextRelevance, Clarity, Answerability).
        /// </summary>
        public RubricScoresDto RubricScores { get; set; } = new RubricScoresDto();

        /// <summary>
        /// Tổng điểm (Ví dụ: cộng tổng các điểm Rubric lại).
        /// </summary>
        public int OverallScore { get; set; }

        /// <summary>
        /// Chuỗi G_q = [Node_1 → Node_2 → … → Node_n] được trích xuất từ Câu hỏi
        /// sau khi ánh xạ ngữ nghĩa bằng Gemini Embedding.
        /// </summary>
        public List<ExtractedPathStepDto> ExtractedPath { get; set; } = new();

        /// <summary>
        /// Danh sách các cặp nút bị đứt gãy (chỉ có giá trị khi IsAccurate = false).
        /// </summary>
        public List<BrokenTransitionDto> BrokenTransitions { get; set; } = new();

        /// <summary>
        /// Lời luận tội / chứng minh tổng hợp về tính chính xác của câu hỏi
        /// được sinh bởi Gemini text model.
        /// </summary>
        public string FinalVerdict { get; set; } = string.Empty;
    }

    /// <summary>
    /// Kết quả đánh giá tính chính xác cho một danh sách câu hỏi.
    /// </summary>
    public class BatchAccuracyAssessmentResultDto
    {
        public Guid BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public List<QuestionAccuracyAssessmentResultDto> QuestionResults { get; set; } = new();
    }

    /// <summary>
    /// Kết quả đánh giá tính chính xác của một câu hỏi riêng lẻ trong danh sách.
    /// </summary>
    public class QuestionAccuracyAssessmentResultDto
    {
        public string Question { get; set; } = string.Empty;
        public AccuracyAssessmentResultDto AccuracyResult { get; set; } = new();
    }
}
