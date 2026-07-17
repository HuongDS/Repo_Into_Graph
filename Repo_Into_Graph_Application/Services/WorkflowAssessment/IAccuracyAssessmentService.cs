using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    /// <summary>
    /// Service thực thi Pipeline 2 bước đánh giá tính chính xác của Câu hỏi nghiệp vụ:
    ///   Bước 1 – Semantic-to-Node Mapping (Gemini Embedding thực)
    ///   Bước 2 – Graph Connection Verification (Path Alignment)
    /// và tổng hợp <c>FinalVerdict</c> bằng Gemini text generation.
    /// </summary>
    public interface IAccuracyAssessmentService
    {
        /// <summary>
        /// Nhận <see cref="AccuracyAssessmentRequestDto"/> chứa Câu hỏi + WorkflowData
        /// (nodes + edges), thực thi Pipeline và trả về kết quả đánh giá chi tiết.
        /// </summary>
        Task<AccuracyAssessmentResultDto> AssessAccuracyAsync(AccuracyAssessmentRequestDto request);
        Task<BatchAccuracyAssessmentResultDto> AssessAccuracyBatchAsync(Repo_Into_Graph_Application.Dtos.QuestionGenerate.GenerateQuestionsResponse response, WorkflowDataDto workflowData);
    }
}
