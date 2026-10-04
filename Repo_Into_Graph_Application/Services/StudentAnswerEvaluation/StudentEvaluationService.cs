using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.StudentAnswerEvaluation;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate;

namespace Repo_Into_Graph_Application.Services.StudentAnswerEvaluation
{
    public class StudentEvaluationService : IStudentEvaluationService
    {
        private readonly ICodeQueryable _codeQueryable;
        private readonly IEvaluationLlmService _evaluationLlmService;
        private readonly IWorkflowAssessmentService _workflowAssessmentService;

        public StudentEvaluationService(ICodeQueryable codeQueryable, IEvaluationLlmService evaluationLlmService,
            IWorkflowAssessmentService workflowAssessmentService)
        {
            _codeQueryable = codeQueryable;
            _evaluationLlmService = evaluationLlmService;
            _workflowAssessmentService = workflowAssessmentService;
        }

        public async Task<string> EvaluateStudentAnswerAsync(StudentEvaluationRequestDto request)
        {
            var context = String.Empty;
            switch (request.ContextModel.ToLower())
            {
                case "raw-code":
                    var codeFlow = await _codeQueryable.GetCodeFlowAsync(request.BusinessId);

                    if (codeFlow != null && codeFlow.Methods != null && codeFlow.Methods.Any())
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Dưới đây là mã nguồn của nghiệp vụ:");
                        foreach (var method in codeFlow.Methods)
                        {
                            sb.AppendLine($"(Lớp: {method.ClassName}) - Hàm: {method.MethodName}");
                            sb.AppendLine(method.SourceCode);
                            sb.AppendLine();
                        }
                        context = sb.ToString();
                    }
                    else
                    {
                        context = "Không tìm thấy source code cho nghiệp vụ này.";
                    }
                    break;
                case "graph":
                    var businessGraph = await _workflowAssessmentService.GetBusinessWorkflowGraphAsync(request.BusinessId);
                    if (businessGraph != null && businessGraph.Nodes.Any())
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Dưới đây là đồ thị luồng xử lý của nghiệp vụ " + businessGraph.BusinessName + ":");
                        sb.AppendLine("Các nút - nodes trong đồ thị:");
                        foreach (var node in businessGraph.Nodes)
                        {
                            sb.AppendLine($"- NodeId: {node.Id}| Tên: {node.Name}| Mô tả: {node.Description} | Phân loại: {node.Type}");
                        }

                        sb.AppendLine("Các cạnh - edges trong đồ thị:");
                        foreach (var edge in businessGraph.Edges)
                        {
                            sb.AppendLine($"- Từ {edge.FromNodeId} -> Đến {edge.ToNodeId} | Điều kiện: {(string.IsNullOrEmpty(edge.Condition) ? "Không có" : edge.Condition)}");
                        }
                        context = sb.ToString();
                    }
                    break;
                default:
                    context = "Chế độ ContextMode không hợp lệ. Vui lòng chọn raw, graph, hoặc hybrid.";
                    break;
            }

            string systemPrompt = @"Bạn là một Giảng viên lập trình cấp cao, thông minh và cực kỳ khách quan. Nhiệm vụ của bạn là chấm điểm câu trả lời của sinh viên.
QUY TẮC TỐI THƯỢNG: 
1. Mã nguồn/Ngữ cảnh (Context) được cung cấp là CHÂN LÝ TUYỆT ĐỐI. 
2. Nếu câu trả lời của sinh viên phân tích đúng với bản chất của mã nguồn nhưng dùng từ ngữ khác với 'Đáp án mẫu', hãy lấy mã nguồn làm chuẩn và vẫn cho điểm tốt. 
3. TUYỆT ĐỐI KHÔNG giải thích dài dòng ở ngoài, CHỈ trả về dữ liệu dưới định dạng JSON được yêu cầu.";

            string userPrompt = $@"
ĐÂY LÀ CÁC THÔNG TIN ĐỂ CHẤM ĐIỂM:
[1. NGỮ CẢNH (MÃ NGUỒN HOẶC ĐỒ THỊ CHÂN LÝ)]
{context}
[2. CÂU HỎI CỦA BÀI TẬP]
{request.Question}
[3. ĐÁP ÁN MẪU / TIÊU CHÍ (Dùng để tham khảo ý nghĩa và kỳ vọng)]
{request.ReferenceMaterial}
[4. CÂU TRẢ LỜI THỰC TẾ CỦA SINH VIÊN]
{request.StudentAnswer}
---
YÊU CẦU ĐẦU RA (BẮT BUỘC TRẢ VỀ ĐÚNG FORMAT):
{BuildJsonFormatRule(request.FeedbackFormat, request.MaxScore)}
";

            string rawResponse = await _evaluationLlmService.EvaluateWithLlmAsync(systemPrompt, userPrompt);

            string cleanJson = rawResponse.Trim();

            if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleanJson = cleanJson.Substring(7); // Cắt bỏ "```json" ở đầu
                if (cleanJson.EndsWith("```"))
                {
                    cleanJson = cleanJson.Substring(0, cleanJson.Length - 3); // Cắt bỏ "```" ở cuối
                }
            }
            else if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Substring(3);
                if (cleanJson.EndsWith("```"))
                {
                    cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                }
            }
            return cleanJson.Trim();
        }

        private string BuildJsonFormatRule(string feedbackFormat, double maxScore)
        {
            // Bắt lỗi nếu người dùng không truyền format, ta mặc định là Sandwich
            if (string.IsNullOrWhiteSpace(feedbackFormat))
                feedbackFormat = "sandwich";

            switch (feedbackFormat.ToLower())
            {
                case "sandwich":
                    return $@"
Hãy trả về ĐÚNG MỘT khối JSON có cấu trúc như sau (không thêm bất kỳ văn bản giải thích nào bên ngoài):
```json
{{
  ""score"": [Điểm số chấm trên thang điểm {maxScore}],
  ""feedback_type"": ""Sandwich"",
  ""positives"": ""[Khen ngợi những điểm sinh viên đã trả lời đúng hoặc có tư duy tốt]"",
  ""constructive_criticism"": ""[Chỉ ra các điểm sai, thiếu sót hoặc mâu thuẫn với mã nguồn/đáp án]"",
  ""next_steps"": ""[Đưa ra lời khuyên, hướng dẫn sinh viên cách cải thiện]""
}}
```";

                case "rubric":
                    return $@"
Hãy trả về ĐÚNG MỘT khối JSON chứa danh sách các tiêu chí đánh giá có cấu trúc như sau (không thêm văn bản ngoài):
```json
{{
  ""feedback_type"": ""Rubric"",
  ""total_score"": [Tổng điểm trên thang {maxScore}],
  ""criteria"": [
    {{
      ""name"": ""[Tên tiêu chí 1 (vd: Tính chính xác so với Code, Khả năng lập luận, ...)]"",
      ""score"": [Điểm của tiêu chí này],
      ""comment"": ""[Nhận xét chi tiết cho tiêu chí này]""
    }},
    {{
      ""name"": ""[Tên tiêu chí 2]"",
      ""score"": [Điểm của tiêu chí này],
      ""comment"": ""[Nhận xét]""
    }}
  ]
}}
```";

                case "explainable":
                    return $@"
Hãy trả về ĐÚNG MỘT khối JSON có cấu trúc giải thích chi tiết như sau (không thêm văn bản ngoài):
```json
{{
  ""score"": [Điểm số trên thang {maxScore}],
  ""feedback_type"": ""Explainable"",
  ""code_evidence"": ""[Trích dẫn cụ thể các dòng code hoặc tên hàm, điều kiện từ ngữ cảnh chứng minh cho nhận xét]"",
  ""logic_explanation"": ""[Giải thích cặn kẽ tại sao lại chấm mức điểm như vậy dựa trên sự đối chiếu giữa câu trả lời và bằng chứng]""
}}
```";

                default:
                    // Đề phòng truyền bậy bạ
                    return $@"
Hãy trả về ĐÚNG MỘT khối JSON như sau:
```json
{{
  ""score"": [0.0 - {maxScore}],
  ""feedback_type"": ""Unknown"",
  ""comment"": ""[Nhận xét chung về câu trả lời]""
}}
```";
            }
        }

    }
}
