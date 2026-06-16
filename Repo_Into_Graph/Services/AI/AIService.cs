using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Google.GenAI;
using Google.GenAI.Types;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.QuestionGenerate;

namespace Repo_Into_Graph.Repo_Into_Graph.Services.AI
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
    }
}