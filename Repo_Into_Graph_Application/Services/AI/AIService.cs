using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Google.GenAI;
using Google.GenAI.Types;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using Repo_Into_Graph_DataAccess.Models.BusinessFlows;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_Application.Dtos.QuestionEvalution;


namespace Repo_Into_Graph_Application.Services.AI
{
    public class AIService : IAIService
    {
        private readonly Client _client;

        public AIService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            var apiKey = configuration["GeminiConfig:ApiKey"];

            var clientOptions = new ClientOptions
            {
                HttpClientFactory = () => httpClientFactory.CreateClient()
            };

            _client = new Client(apiKey: apiKey, clientOptions: clientOptions);
        }

        public async Task<IEnumerable<QuestionEvaluationResultDto>> EvaluateQuestionsAsync(string dataFlowMermaidGraph, string codeBuilder, IEnumerable<GeneratedQuestionDto> generatedQuestions)
        {
            if (generatedQuestions == null || !generatedQuestions.Any())
                return new List<QuestionEvaluationResultDto>();

            var systemInstruction = """
            Bạn là một Chuyên gia Kiểm định Chất lượng Phần mềm (Senior QA Engineer) và là Hội đồng Thẩm định AI phục vụ cho Đề tài Nghiên cứu Khoa học.
            Nhiệm vụ của bạn là phân tích mã nguồn (Source Code) và biểu đồ luồng dữ liệu (Data Flow Mermaid) được cung cấp để làm CHÂN LÝ (Ground Truth).
            Sau đó, hãy đánh giá chất lượng các cặp Câu hỏi (question) và Câu trả lời gợi ý (suggestedAnswer) được sinh ra tự động từ hệ thống xem có khớp với Chân lý hay không.
            
            Hãy duyệt qua từng câu hỏi trong danh sách và chấm điểm từ 1 đến 10 dựa trên 3 tiêu chí cốt lõi:
            1. factual_correctness: Độ chính xác thực tế của câu trả lời so với Logic xử lý trong Source Code và luồng đi của Data Flow Mermaid.
            2. relevance_completeness: Điểm độ liên quan, giải quyết trọn vẹn câu hỏi, không viết dài dòng lan man.
            3. technical_clarity: Việc sử dụng chính xác các thuật ngữ công nghệ chuyên ngành (ví dụ: batch insert, validation rules, commit...).
            """;

            string questionsJsonContext = JsonSerializer.Serialize(generatedQuestions, new JsonSerializerOptions { WriteIndented = true });

            var prompt = new StringBuilder();
            prompt.AppendLine("--- SOURCE CODE CHI TIẾT (GROUND TRUTH) ---");
            prompt.AppendLine(codeBuilder);
            prompt.AppendLine();

            prompt.AppendLine("--- BIỂU ĐỒ LUỒNG ĐỐI CHIẾU (DATA FLOW MERMAID) ---");
            prompt.AppendLine(dataFlowMermaidGraph);
            prompt.AppendLine();

            prompt.AppendLine("--- DANH SÁCH CẶP CÂU HỎI VÀ CÂU TRẢ LỜI CẦN CHẤM ĐIỂM ---");
            prompt.AppendLine(questionsJsonContext);
            prompt.AppendLine();
            prompt.AppendLine("Hãy đánh giá toàn bộ các câu hỏi trên. Xuất ra kết quả là một JSON Array với cấu trúc chứa các thuộc tính: question, suggestedAnswer, scores (gồm factual_correctness, relevance_completeness, technical_clarity, average_total_score), và evaluation_details (gồm factual_correctness_reason, relevance_completeness_reason, technical_clarity_reason).");

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = systemInstruction }]
                },
                Temperature = 0.15f,
                ResponseMimeType = "application/json"
            };

            int maxRetries = 3;
            int delaySeconds = 3;
            GenerateContentResponse? response = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    response = await _client.Models.GenerateContentAsync(
                        model: "gemini-3.1-flash-lite",
                        contents: prompt.ToString(),
                        config: config
                    );
                    break;
                }
                catch (ClientError ex) when (ex.StatusCode == 429 || ex.StatusCode == 503 || ex.StatusCode == 500)
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AiJudge] Transient error {ex.StatusCode}. Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AiJudge] Rate limit error. Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
            }

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI Judge API did not return any evaluation text response.");

            string aiJsonText = response.Text.Trim();

            aiJsonText = aiJsonText.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var evaluationResults = JsonSerializer.Deserialize<List<QuestionEvaluationResultDto>>(aiJsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return evaluationResults ?? new List<QuestionEvaluationResultDto>();
            }
            catch (JsonException ex)
            {
                throw new Exception($"AI Judge trả về cấu trúc JSON không khớp với DTO đánh giá. Nội dung: {aiJsonText}. Chi tiết lỗi: {ex.Message}", ex);
            }
        }
        

        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestions(
            int numberOfQuestions,
            GenerateQuestionsRequest request,
            string codeBuilder,
            string contextBuilder)
        {
            var systemInstruction = """
            Bạn là một AI trợ lý thiết kế riêng cho giảng viên đại học để chấm thi vấn đáp (viva/oral exam) các đồ án lập trình của sinh viên.
            Nhiệm vụ của bạn là phân tích mã nguồn và đồ thị cuộc gọi (call graph) được cung cấp để tạo ra danh sách câu hỏi.
            Các câu hỏi phải kiểm tra được độ hiểu sâu của sinh viên về luồng nghiệp vụ.
            """;

            var prompt = new StringBuilder();
            prompt.AppendLine($"Số câu hỏi : {numberOfQuestions}");
            prompt.AppendLine($"Mức độ khó: {request.Difficulty}");

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                prompt.AppendLine($"Hướng dẫn bổ sung/vùng tập trung: {request.Description}");
            }

            prompt.AppendLine();
            prompt.AppendLine("--- SOURCE CODE ---");
            prompt.AppendLine(codeBuilder);
            prompt.AppendLine();
            prompt.AppendLine("--- CALL GRAPH CONTEXT ---");
            prompt.AppendLine(contextBuilder);
            prompt.AppendLine();
            prompt.AppendLine("Hãy trả về một danh sách các câu hỏi theo cấu trúc object gồm các key: question, suggestedAnswer, difficulty.");

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = systemInstruction }]
                },
                Temperature = 0.3f,
                ResponseMimeType = "application/json"
            };

            int maxRetries = 3;
            int delaySeconds = 3;
            GenerateContentResponse? response = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Gọi API thông qua SDK chính thức
                    response = await _client.Models.GenerateContentAsync(
                        model: "gemini-3.1-flash-lite",
                        contents: prompt.ToString(),
                        config: config
                    );
                    break;
                }
                catch (ClientError ex) when (ex.StatusCode == 429 || ex.StatusCode == 503 || ex.StatusCode == 500)
                {
                    if (attempt == maxRetries) throw;

                    Console.WriteLine($"⚠️ [AIService] Transient error {ex.StatusCode}. Retrying in {delaySeconds} seconds... (Attempt {attempt} of {maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    if (attempt == maxRetries) throw;

                    Console.WriteLine($"⚠️ [AIService] Rate limit error. Retrying in {delaySeconds} seconds... (Attempt {attempt} of {maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
            }

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI API did not return any text response.");

            string aiJsonText = response.Text.Trim();

            try
            {
                var questions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(aiJsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return questions ?? new List<GeneratedQuestionDto>();
            }
            catch (JsonException ex)
            {
                throw new Exception($"AI trả về cấu trúc JSON không khớp với DTO. Nội dung: {aiJsonText}. Chi tiết lỗi: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateQuestionsFromBusinessFlowAsync(
            BusinessFlow businessFlow,
            int numberOfQuestions,
            string difficulty,
            string? additionalContext = null,
            IEnumerable<FewShotExample>? fewShotExamples = null)
        {
            if (businessFlow == null) throw new ArgumentNullException(nameof(businessFlow));

            string systemInstruction = @"
            Bạn là một Senior Business Analyst (BA) và Solution Architect chuyên nghiệp. 
Nhiệm vụ của bạn là phân tích mã nguồn hoặc đồ thị code được cung cấp và TRÍCH XUẤT LUỒNG NGHIỆP VỤ (Business Flow), tuyệt đối không giải thích kỹ thuật code.

HƯỚNG DẪN PHÂN TÍCH:
1. Mục tiêu nghiệp vụ: Tính năng này giúp người dùng cuối (hoặc hệ thống) giải quyết bài toán gì?
2. Luồng xử lý chức năng (Functional Flow): Mô tả các bước đi của dữ liệu và hành động theo góc nhìn nghiệp vụ (Ví dụ: Bước 1: Tiếp nhận yêu cầu chấm bài -> Bước 2: Kiểm tra điều kiện hợp lệ -> Bước 3: Đánh giá tiêu chí -> Bước 4: Trả kết quả).
3. Quy tắc nghiệp vụ (Business Rules): Chỉ ra các điều kiện logic ràng buộc nghiệp vụ có trong code (Ví dụ: Nếu điểm dưới 5 thì xếp loại trượt, phải có token hợp lệ mới được kích hoạt luồng...).

QUY TẮC CẤM (CRITICAL CONSTRAINTS):
- KHÔNG giải thích cú pháp C#, không nhắc đến tên hàm, tên biến, các khối try-catch, vòng lặp (for/while), hay cách tối ưu code.
- Sử dụng thuật ngữ nghiệp vụ (Ví dụ: thay vì nói 'hàm trả về chuỗi JSON', hãy nói 'hệ thống phản hồi thông tin kết quả dưới dạng cấu trúc').
- Trả về kết quả ngắn gọn, scannable (dùng bullet points), tập trung vào giá trị logic của luồng.
            ";

            // Build the ordered call chain as a readable list
            var stepLines = new StringBuilder();
            if (businessFlow.Steps != null && businessFlow.Steps.Count > 0)
            {
                foreach (var step in businessFlow.Steps.OrderBy(s => s.StepOrder))
                {
                    stepLines.AppendLine($"  [{step.StepOrder}] {step.CallerClass}.{step.CallerMethod} --> {step.CalleeClass}.{step.CalleeMethod}");
                }
            }
            else
            {
                stepLines.AppendLine("  (Không có dữ liệu bước gọi)");
            }

            var prompt = new StringBuilder();
            prompt.AppendLine($"Số câu hỏi cần sinh: {numberOfQuestions}");
            prompt.AppendLine($"Mức độ khó: {difficulty}");
            prompt.AppendLine();
            prompt.AppendLine("--- THÔNG TIN BUSINESS FLOW ---");
            prompt.AppendLine($"Tên luồng: {businessFlow.Name}");
            prompt.AppendLine($"Entry Point (API entry): {businessFlow.EntryPoint}");
            prompt.AppendLine();
            prompt.AppendLine("--- CHUỖI BƯỚC GỌI (call chain theo thứ tự) ---");
            prompt.Append(stepLines);
            prompt.AppendLine();

            if (!string.IsNullOrWhiteSpace(businessFlow.DataFlowMermaidGraph))
            {
                prompt.AppendLine("--- DATA FLOW MERMAID DIAGRAM (Biểu đồ luồng) ---");
                prompt.AppendLine(businessFlow.DataFlowMermaidGraph);
                prompt.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(additionalContext))
            {
                prompt.AppendLine("--- HƯỚNG DẪN BỔ SUNG TỪ GIẢNG VIÊN ---");
                prompt.AppendLine(additionalContext);
                prompt.AppendLine();
            }

            // Nhúng few-shot examples vào prompt
            var examplesList = fewShotExamples?.ToList();
            if (examplesList != null && examplesList.Count > 0)
            {
                prompt.AppendLine("--- VÍ DỤ CÂU HỎI CHUẨN CỦA GIẢNG VIÊN (FEW-SHOT EXAMPLES) ---");
                prompt.AppendLine("Dưới đây là các câu hỏi mẫu do giảng viên cung cấp. Hãy học theo phong cách, độ sâu nghiệp vụ và cấu trúc câu trả lời của chúng:");
                prompt.AppendLine();
                for (int i = 0; i < examplesList.Count; i++)
                {
                    var ex = examplesList[i];
                    prompt.AppendLine($"[Ví dụ {i + 1}]");
                    prompt.AppendLine($"  Câu hỏi   : {ex.Question}");
                    prompt.AppendLine($"  Đáp án    : {ex.SuggestedAnswer}");
                    prompt.AppendLine($"  Mức độ    : {ex.Difficulty}");
                    if (!string.IsNullOrWhiteSpace(ex.Tag))
                        prompt.AppendLine($"  Nhãn      : {ex.Tag}");
                    prompt.AppendLine();
                }
            }

            prompt.AppendLine("Hãy trả về một danh sách các câu hỏi theo cấu trúc object gồm các key: question, suggestedAnswer, difficulty.");

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = systemInstruction }]
                },
                Temperature = 0.4f,
                ResponseMimeType = "application/json"
            };

            int maxRetries = 3;
            int delaySeconds = 3;
            GenerateContentResponse? response = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    response = await _client.Models.GenerateContentAsync(
                        model: "gemini-3.1-flash-lite",
                        contents: prompt.ToString(),
                        config: config
                    );
                    break;
                }
                catch (ClientError ex) when (ex.StatusCode == 429 || ex.StatusCode == 503 || ex.StatusCode == 500)
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AIService] Transient error {ex.StatusCode} (BusinessFlow). Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AIService] Rate limit (BusinessFlow). Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
            }

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI API did not return any text response for business flow.");

            string aiJsonText = response.Text.Trim();

            try
            {
                var questions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(aiJsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return questions ?? new List<GeneratedQuestionDto>();
            }
            catch (JsonException ex)
            {
                throw new Exception($"AI trả về cấu trúc JSON không khớp với DTO. Nội dung: {aiJsonText}. Chi tiết lỗi: {ex.Message}", ex);
            }
        }
    }
}




