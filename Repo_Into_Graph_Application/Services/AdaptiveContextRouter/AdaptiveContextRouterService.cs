using Microsoft.Extensions.Configuration;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.AdaptiveContextRouter
{
    public class AdaptiveContextRouterService : IAdaptiveContextRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly string _pythonServiceUrl;

        public AdaptiveContextRouterService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _pythonServiceUrl = configuration["PythonMicroserviceUrl"] ?? "http://localhost:8000";
        }

        public async Task<RouterDecisionDto> EvaluateCodeContextAsync(RouterRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceCode))
            {
                return new RouterDecisionDto
                {
                    IsValidSyntax = false,
                    Message = "Mã nguồn đầu vào không được để trống."
                };
            }

            try
            {
                // 2 & 3. Gọi qua FastAPI Python để Parse AST, đếm SLOC, đếm V(G)
                var apiUrl = $"{_pythonServiceUrl}/api/analyze-context";
                var pythonResponse = await _httpClient.PostAsJsonAsync(apiUrl, new
                {
                    code = request.SourceCode,
                    language = request.Language
                });

                if (!pythonResponse.IsSuccessStatusCode)
                {
                    return new RouterDecisionDto
                    {
                        IsValidSyntax = false,
                        Message = $"Lỗi giao tiếp với Python Engine: {pythonResponse.ReasonPhrase}"
                    };
                }

                var analysisResult = await pythonResponse.Content.ReadFromJsonAsync<PythonAnalyzeResponse>();
                if (analysisResult == null)
                {
                    return new RouterDecisionDto
                    {
                        IsValidSyntax = false,
                        Message = "Dữ liệu trả về từ Python Engine bị null hoặc sai định dạng."
                    };
                }

                if (!analysisResult.IsValid)
                {
                    return new RouterDecisionDto
                    {
                        IsValidSyntax = false,
                        Sloc = analysisResult.Sloc,
                        Vg = analysisResult.Vg,
                        Message = "Mã nguồn có chứa lỗi cú pháp biên dịch (Syntax Error)."
                    };
                }

                // 4. Phân luồng (Decision Routing)
                var decision = new RouterDecisionDto
                {
                    IsValidSyntax = true,
                    Sloc = analysisResult.Sloc,
                    Vg = analysisResult.Vg
                };

                if (decision.Sloc < 25 || decision.Vg <= 2)
                {
                    decision.SelectedRoute = RoutingType.RawCode;
                    decision.Message = "Hàm đơn giản, định tuyến sử dụng Mã Nguồn Gốc (Raw Code).";
                }
                else
                {
                    decision.SelectedRoute = RoutingType.HybridGraph;
                    decision.Message = "Hàm phức tạp, định tuyến sang Tầng 2: Ngữ cảnh Lai (Hybrid Graph).";
                }

                return decision;
            }
            catch (Exception ex)
            {
                return new RouterDecisionDto
                {
                    IsValidSyntax = false,
                    Message = $"Có lỗi ngoại lệ xảy ra trong quá trình đánh giá: {ex.Message}"
                };
            }
        }
    }
}