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
    public class DeepSeekEvaluationService : IEvaluationLlmService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DeepSeekEvaluationService> _logger;
        private readonly string _deepSeekApiKey;

        public DeepSeekEvaluationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<DeepSeekEvaluationService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deepSeekApiKey = configuration["DeepSeekConfig:ApiKey"]
                          ?? throw new InvalidOperationException("Thiếu cấu hình DeepSeekConfig:ApiKey.");
        }

        public async Task<string> EvaluateWithLlmAsync(string systemPrompt, string userPrompt)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _deepSeekApiKey);

            var requestBody = new
            {
                // Sử dụng model deepseek-chat cho tác vụ lập luận, đánh giá. Vừa rẻ vừa vô đối.
                model = "deepseek-chat",
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

            int maxRetries = 5;
            int delaySeconds = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // API endpoint của DeepSeek
                    var response = await client.PostAsync("https://api.deepseek.com/chat/completions", jsonContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        _logger.LogError("DeepSeek API Call Failed: {StatusCode} - {ErrorMsg}", response.StatusCode, errorMsg);
                        
                        // Xử lý tự động đợi khi bị dính Rate Limit (429)
                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == maxRetries) throw new Exception($"DeepSeek Rate Limit exceeded: {errorMsg}");
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
                        _logger.LogError(ex, "DeepSeekEvaluationService failed after {Retries} attempts.", maxRetries);
                        return string.Empty;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }

            return string.Empty;
        }
    }
}
