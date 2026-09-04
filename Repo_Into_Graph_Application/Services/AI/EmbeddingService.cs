using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Repo_Into_Graph_Application.Services.AI
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _cohereApiKey;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<EmbeddingService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _cohereApiKey = (configuration ?? throw new ArgumentNullException(nameof(configuration)))
                .GetSection("CohereConfig:ApiKey").Value
                ?? throw new InvalidOperationException("Thiếu cấu hình AI_Models:Cohere:ApiKey");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<double[][]> EmbedBatchAsync(List<string> texts, string inputType)
        {
            var allEmbeddings = new List<double[]>();
            int batchSize = 96;

            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var chunkTexts = texts.Skip(i).Take(batchSize).ToList();

                int retries = 3;
                int delay = 2;
                bool success = false;

                for (int attempt = 1; attempt <= retries; attempt++)
                {
                    try
                    {
                        var client = _httpClientFactory.CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cohereApiKey);

                        var payload = new
                        {
                            texts = chunkTexts,
                            model = "embed-multilingual-v3.0",
                            input_type = inputType,
                            embedding_types = new[] { "float" }
                        };

                        var content = new StringContent(
                            JsonSerializer.Serialize(payload),
                            Encoding.UTF8,
                            "application/json");

                        var response = await client.PostAsync("https://api.cohere.com/v2/embed", content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            using var document = JsonDocument.Parse(responseString);
                            var root = document.RootElement;

                            if (root.TryGetProperty("embeddings", out var embeddingsElement) &&
                                embeddingsElement.TryGetProperty("float", out var arrayProp) &&
                                arrayProp.ValueKind == JsonValueKind.Array)
                            {
                                var embeddings = arrayProp.EnumerateArray()
                                    .Select(e => e.EnumerateArray().Select(v => v.GetDouble()).ToArray())
                                    .ToList();

                                allEmbeddings.AddRange(embeddings);
                                success = true;
                                break;
                            }
                        }
                        else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                        {
                            if (attempt == retries) throw new InvalidOperationException("Cohere Rate Limit Exceeded");

                            int waitSeconds = delay;
                            if (response.Headers.RetryAfter?.Delta.HasValue == true)
                            {
                                waitSeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                            }

                            _logger.LogWarning("[EmbedBatch] Rate limit 429. Thử lại sau {D}s... (Attempt {A}/{R})",
                                waitSeconds, attempt, retries);

                            await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                            delay = Math.Max(delay * 2, 3);
                        }
                        else
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            _logger.LogError("[EmbedBatch] Lỗi gọi Cohere: {Error}", error);
                            throw new Exception($"Cohere Error: {response.StatusCode} - {error}");
                        }
                    }
                    catch (Exception ex) when (attempt < retries && !(ex is InvalidOperationException))
                    {
                        _logger.LogWarning(ex, "[EmbedBatch] Lỗi mạng, thử lại lần {A}/{R}", attempt, retries);
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                        delay *= 2;
                    }
                }

                if (!success)
                {
                    throw new Exception("Lỗi sau nhiều lần thử gọi Cohere Embed.");
                }
            }

            return allEmbeddings.ToArray();
        }

        public double CosineSimilarity(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0.0;
            double dot = 0.0, magA = 0.0, magB = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            return magA == 0.0 || magB == 0.0 ? 0.0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
        }
    }
}
