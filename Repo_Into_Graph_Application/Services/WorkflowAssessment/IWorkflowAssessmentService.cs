using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    /// <summary>
    /// Hợp đồng (Contract) của WorkflowAssessmentService.
    /// </summary>
    public interface IWorkflowAssessmentService
    {
        /// <summary>
        /// Thực thi Pipeline 3 bước để đánh giá một Câu hỏi nghiệp vụ (gọi thủ công).
        /// </summary>
        Task<AssessmentResultDto> AssessAsync(AssessmentRequestDto request);

        /// <summary>
        /// Nhận <see cref="GenerateQuestionsResponse"/>, tự động query DB xây dựng đồ thị
        /// Workflow (SelectedWorkflow) và GlobalGraph của Business tương ứng, sau đó
        /// chạy Pipeline 3 bước cho từng câu hỏi và trả về kết quả tổng hợp.
        /// </summary>
        /// <param name="response">
        ///   Output từ QuestionGenerateService – chứa BusinessId và toàn bộ GeneratedQuestionDtos.
        /// </param>
        Task<CoverageAssessmentResultDto> AssessCoverageAsync(GenerateQuestionsResponse response);



        /// <summary>
        /// Lấy cấu trúc đồ thị (Nodes và Edges) của một Business Flow dưới dạng BusinessWorkflowGraphDto.
        /// </summary>
        Task<BusinessWorkflowGraphDto> GetBusinessWorkflowGraphAsync(Guid businessId);

        /// <summary>
        /// Lấy cấu trúc đồ thị luồng nghiệp vụ (WorkflowDataDto) từ CSDL dựa trên BusinessId.
        /// </summary>
        Task<WorkflowDataDto> GetWorkflowDataAsync(Guid businessId);
    }
}
