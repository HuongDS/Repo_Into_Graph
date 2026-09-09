using System.Text;

namespace Repo_Into_Graph_Application.Services.LLMOrchestrator
{
    public class PromptBuilderService : IPromptBuilderService
    {
        public string BuildSystemPrompt(int numberOfQuestions = 5, string difficulty = "Medium")
        {
            return $@"Bạn là chuyên gia giáo dục phần mềm. Dựa vào Call Graph và sơ đồ rẽ nhánh nội bộ dưới đây, hãy tạo {numberOfQuestions} câu hỏi tình huống với mức độ khó là '{difficulty}' để kiểm tra khả năng theo dõi luồng dữ liệu của sinh viên. 
TUYỆT ĐỐI tuân thủ định dạng JSON đầu ra dưới đây (không chứa markdown ```json):
{{
  ""questions"": [
    {{
      ""questionId"": ""q1"",
      ""questionText"": ""Nội dung câu hỏi tình huống ở đây?"",
      ""questionType"": ""Trắc nghiệm"",
      ""options"": [""A. ..."", ""B. ..."", ""C. ..."", ""D. ...""],
      ""correctAnswer"": ""A. ..."",
      ""explanation"": ""Giải thích vì sao chọn đáp án này dựa trên luồng."",
      ""targetNode"": ""Tên hàm/node bị ảnh hưởng hoặc cần kiểm tra"",
      ""difficultyLevel"": ""{difficulty}""
    }}
  ],
  ""metaData"": {{
    ""totalGenerated"": {numberOfQuestions},
    ""topic"": ""Software Engineering Context""
  }}
}}";
        }

        public string BuildFinalPayload(string workflowOverview, string aggregatedNodesContext)
        {
            var builder = new StringBuilder();
            
            builder.AppendLine("=== PHẦN 1: BỨC TRANH TOÀN CẢNH (WORKFLOW OVERVIEW) ===");
            builder.AppendLine(workflowOverview);
            builder.AppendLine();
            
            builder.AppendLine("=== PHẦN 2: CHI TIẾT TỪNG TRẠM (NODE DETAILS) ===");
            builder.AppendLine(aggregatedNodesContext);
            builder.AppendLine();

            return builder.ToString();
        }
    }
}
