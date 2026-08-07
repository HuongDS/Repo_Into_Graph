using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Repo_Into_Graph_Application.Services.WorkflowAssessment.AccuracyEvaluate
{
    public class GroqEvaluationService : IEvaluationLlmService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GroqEvaluationService> _logger;
        private readonly string _groqApiKey;

        public GroqEvaluationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GroqEvaluationService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _groqApiKey = configuration["GroqConfig:ApiKey"]
                          ?? throw new InvalidOperationException("Thiếu cấu hình GroqConfig:ApiKey.");
        }

        public async Task<string> EvaluateWithLlmAsync(string systemPrompt, string userPrompt)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

            var requestBody = new
            {
                // Dùng model thông minh nhất (70B) làm Giám khảo để loại bỏ Bias
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2,
                response_format = new { type = "json_object" }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            int maxRetries = 8; // Tăng số lần thử lên 8 lần để đủ thời gian reset Token Bucket của Groq
            int delaySeconds = 4;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", jsonContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Groq API Call Failed: {StatusCode} - {ErrorMsg}", response.StatusCode, errorMsg);
                        
                        // Xử lý tự động đợi khi bị dính Rate Limit (429)
                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == maxRetries) throw new Exception($"Groq Rate Limit exceeded: {errorMsg}");
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                            delaySeconds *= 2; // Cấp số nhân thời gian đợi
                            continue;
                        }
                        
                        response.EnsureSuccessStatusCode();
                    }

                    var responseString = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseString);

                    var contentText = result
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return contentText ?? string.Empty;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "GroqEvaluationService failed after {Retries} attempts.", maxRetries);
                        return string.Empty;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }

            return string.Empty;
        }
    }
}
