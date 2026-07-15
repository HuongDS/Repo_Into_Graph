using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment
{
    /// <summary>
    /// Service tính toán các chỉ số đồ thị để chứng minh và lượng hóa Độ Khó
    /// của một câu hỏi nghiệp vụ dựa trên tập hợp Active Nodes đã được xác thực.
    ///
    /// <para>
    /// Ba chỉ số được tính toán:
    /// <list type="number">
    ///   <item>V(G) – Cyclomatic Complexity = E_q - V_q + 2</item>
    ///   <item>L_q – Impact Path Length = ActiveNodes.Count - 1</item>
    ///   <item>PathType – Loại nhánh kích hoạt dựa trên số DecisionGateway</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IDifficultyAssessmentService
    {
        /// <summary>
        /// Tính toán và phân loại Độ Khó của câu hỏi từ danh sách Active Nodes đã xác thực.
        /// Không phụ thuộc vào Gemini API hay Database — chỉ xử lý toán học thuần túy.
        /// </summary>
        /// <param name="request">
        ///   Danh sách Active Nodes và kích thước đồ thị con G_q.
        /// </param>
        /// <returns>
        ///   <see cref="DifficultyAssessmentResultDto"/> chứa đầy đủ các chỉ số và lời chứng minh.
        /// </returns>
        Task<DifficultyAssessmentResultDto> AssessAsync(DifficultyAssessmentRequestDto request);
    }
}
