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
using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;
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
        

        public async Task<IEnumerable<GeneratedQuestionDto>> GenerateUnifiedQuestionsAsync(
            FeatureModel feature,
            string codeBuilder,
            int numberOfQuestions,
            string difficulty,
            string? additionalContext = null,
            IEnumerable<FewShotExample>? fewShotExamples = null)
        {
            if (feature == null) throw new ArgumentNullException(nameof(feature));

            var systemInstruction = @"
            Bạn là một Technical Leader, Senior Business Analyst (BA) và Solution Architect chuyên nghiệp thiết kế riêng cho giảng viên đại học để chấm thi vấn đáp (viva/oral exam) các đồ án lập trình của sinh viên.
            Nhiệm vụ của bạn là phân tích MÃ NGUỒN CHI TIẾT (Source Code) kết hợp với LUỒNG NGHIỆP VỤ (Business Flow, Call Graph, Mermaid) được cung cấp để TRÍCH XUẤT LUỒNG NGHIỆP VỤ và tạo ra danh sách câu hỏi.
            Các câu hỏi phải kiểm tra được độ hiểu sâu của sinh viên về cả logic code và luồng nghiệp vụ.

            HƯỚNG DẪN TẠO CÂU HỎI:
            1. Hỏi về Luồng xử lý chức năng (Functional Flow): Các bước đi của dữ liệu từ khi tiếp nhận yêu cầu đến khi trả kết quả.
            2. Hỏi về Quy tắc nghiệp vụ (Business Rules): Các điều kiện logic ràng buộc có trong source code.
            3. Hỏi sâu về Kỹ thuật (Technical Detail): Mối quan hệ giữa các class/method trong Call Graph và tại sao lại thiết kế như vậy.

            QUY TẮC CẤM (CRITICAL CONSTRAINTS):
            - KHÔNG giải thích lặp lại cú pháp C# đơn thuần mà không gắn với nghiệp vụ.
            - Trả về kết quả ngắn gọn, súc tích, tập trung vào giá trị cốt lõi.
            - Phải trả về mảng JSON theo đúng định dạng được yêu cầu.
            ";

            // Build the ordered call chain as a readable list
            var stepLines = new StringBuilder();
            if (feature.Steps != null && feature.Steps.Count > 0)
            {
                foreach (var step in feature.Steps.OrderBy(s => s.StepOrder))
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

            prompt.AppendLine("--- THÔNG TIN BUSINESS FLOW CONTEXT ---");
            prompt.AppendLine($"Tên luồng: {feature.Name}");
            prompt.AppendLine($"Entry Point (API entry): {feature.EntryPoint}");
            prompt.AppendLine();
            
            prompt.AppendLine("--- CHUỖI BƯỚC GỌI (Call chain theo thứ tự) ---");
            prompt.Append(stepLines);
            prompt.AppendLine();

            if (!string.IsNullOrWhiteSpace(feature.DataFlowMermaidGraph))
            {
                prompt.AppendLine("--- DATA FLOW MERMAID DIAGRAM ---");
                prompt.AppendLine(feature.DataFlowMermaidGraph);
                prompt.AppendLine();
            }

            prompt.AppendLine("--- SOURCE CODE CHI TIẾT (CODEBASE) ---");
            prompt.AppendLine(codeBuilder);
            prompt.AppendLine();

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
                Temperature = 0.3f, // Cân bằng giữa code và tính sáng tạo của Business
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
                    Console.WriteLine($"⚠️ [AIService] Transient error {ex.StatusCode} (Unified). Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
                {
                    if (attempt == maxRetries) throw;
                    Console.WriteLine($"⚠️ [AIService] Rate limit (Unified). Retrying in {delaySeconds}s... (Attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    delaySeconds *= 2;
                }
            }

            if (response == null || string.IsNullOrEmpty(response.Text))
                throw new Exception("AI API did not return any text response for unified generation.");

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




