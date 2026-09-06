using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator
{
    /// <summary>
    /// TANG 2 - HYBRID CONTEXT GENERATOR (lop dieu phoi).
    ///
    /// Toan bo viec sinh Ngu canh Lai (CFG skeleton + Critical Snippets +
    /// Enriched Metadata + hybrid_prompt) duoc thuc hien trong Python
    /// Microservice, CUNG TIEN TRINH voi Tang 1, tren cay tree-sitter da nap
    /// san trong RAM. Lop nay chi:
    ///     1. Kiem tra dau vao
    ///     2. Goi MOT lan /api/generate-hybrid-context
    ///     3. Tra nguyen ket qua ve cho Orchestrator
    ///
    /// KHONG co bo phan tich du phong viet bang C#: mot de tai nghien cuu can
    /// chat luong prompt nhat quan giua cac lan chay, nen khi service khong san
    /// sang thi bao FAILED ro rang thay vi lang le sinh ra CFG kem chat luong hon.
    /// </summary>
    public class HybridContextGeneratorService : IHybridContextGeneratorService
    {
        private const int TimeoutSeconds = 30;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly string _pythonServiceUrl;
        private readonly ILogger<HybridContextGeneratorService> _logger;

        public HybridContextGeneratorService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<HybridContextGeneratorService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _pythonServiceUrl = (configuration["PythonMicroserviceUrl"] ?? "http://localhost:8000").TrimEnd('/');
        }

        public async Task<HybridContextOutputDto> GenerateAsync(HybridContextInputDto input)
        {
            var stopwatch = Stopwatch.StartNew();

            if (input == null)
            {
                return Failed(string.Empty, string.Empty, string.Empty,
                              "Input rong (null) - khong the sinh ngu canh lai.", 0);
            }

            if (string.IsNullOrWhiteSpace(input.RawSourceCode))
            {
                return Failed(input.ModuleId, input.Language, input.RoutingDecision,
                              "RawSourceCode rong - Tang 2 khong co du lieu de xay dung CFG.",
                              stopwatch.ElapsedMilliseconds);
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

                var response = await _httpClient.PostAsJsonAsync(
                    _pythonServiceUrl + "/api/generate-hybrid-context",
                    new
                    {
                        moduleId = input.ModuleId ?? string.Empty,
                        language = input.Language ?? string.Empty,
                        routingDecision = string.IsNullOrWhiteSpace(input.RoutingDecision)
                            ? "ROUTE_HYBRID"
                            : input.RoutingDecision,
                        rawSourceCode = input.RawSourceCode,
                        metrics = new
                        {
                            sloc = input.Metrics?.Sloc ?? 0,
                            cyclomaticComplexity = input.Metrics?.CyclomaticComplexity ?? 0
                        }
                    },
                    cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return Failed(input.ModuleId, input.Language, input.RoutingDecision,
                        $"Python Microservice tra ve {(int)response.StatusCode} {response.ReasonPhrase}. " +
                        "Kiem tra service da chay tai " + _pythonServiceUrl + " chua.",
                        stopwatch.ElapsedMilliseconds);
                }

                var output = await response.Content.ReadFromJsonAsync<HybridContextOutputDto>(JsonOptions, cts.Token);

                if (output == null)
                {
                    return Failed(input.ModuleId, input.Language, input.RoutingDecision,
                                  "Du lieu tra ve tu Tang 2 bi null hoac sai dinh dang.",
                                  stopwatch.ElapsedMilliseconds);
                }

                stopwatch.Stop();

                _logger?.LogInformation(
                    "[Tang 2] Module '{ModuleId}' | {Language} | Parser: {Parser} | Status: {Status} | " +
                    "Nodes: {Nodes} | Edges: {Edges} | Snippets: {Snippets} | {Elapsed} ms",
                    output.ModuleId, output.Language, output.Parser, output.Status,
                    output.CfgNodes.Count, output.CfgEdges.Count,
                    output.CriticalSnippets.Count, stopwatch.ElapsedMilliseconds);

                return output;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex, "[Tang 2] Loi khi goi Python Microservice cho module '{ModuleId}'",
                                  input.ModuleId);

                return Failed(input.ModuleId, input.Language, input.RoutingDecision,
                              "Khong goi duoc Tang 2 tai " + _pythonServiceUrl + ": " + ex.Message,
                              stopwatch.ElapsedMilliseconds);
            }
        }

        private static HybridContextOutputDto Failed(string? moduleId, string? language,
                                                     string? routingDecision, string message, long elapsedMs)
        {
            return new HybridContextOutputDto
            {
                ModuleId = moduleId ?? string.Empty,
                Language = language ?? string.Empty,
                RouteDecision = string.IsNullOrWhiteSpace(routingDecision) ? "ROUTE_HYBRID" : routingDecision,
                Parser = "tree-sitter",
                Status = "FAILED",
                Message = message,
                ProcessingTimeMs = elapsedMs,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }
}
