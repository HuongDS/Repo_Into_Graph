using Microsoft.Extensions.Configuration;
using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;
using Repo_Into_Graph_Application.Services.HybridContextGenerator;
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
        private readonly IHybridContextGeneratorService _hybridContextGeneratorService;

        public AdaptiveContextRouterService(
            HttpClient httpClient,
            IConfiguration configuration,
            IHybridContextGeneratorService hybridContextGeneratorService)
        {
            _httpClient = httpClient;
            _pythonServiceUrl = configuration["PythonMicroserviceUrl"] ?? "http://localhost:8000";
            _hybridContextGeneratorService = hybridContextGeneratorService;
        }

        public async Task<RouterDecisionDto> EvaluateCodeContextAsync(RouterRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceCode))
            {
                return new RouterDecisionDto
                {
                    IsValidSyntax = false,
                    Message = "Ma nguon dau vao khong duoc de trong."
                };
            }

            try
            {
                // --- TANG 1: Goi Python Microservice de phan tich AST, SLOC, V(G) ---
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
                        Message = $"Loi giao tiep voi Python Engine: {pythonResponse.ReasonPhrase}"
                    };
                }

                var analysisResult = await pythonResponse.Content.ReadFromJsonAsync<PythonAnalyzeResponse>();
                if (analysisResult == null)
                {
                    return new RouterDecisionDto
                    {
                        IsValidSyntax = false,
                        Message = "Du lieu tra ve tu Python Engine bi null hoac sai dinh dang."
                    };
                }

                if (!analysisResult.IsValid)
                {
                    return new RouterDecisionDto
                    {
                        IsValidSyntax = false,
                        Sloc = analysisResult.Sloc,
                        Vg = analysisResult.Vg,
                        Message = "Ma nguon co chua loi cu phap bien dich (Syntax Error)."
                    };
                }

                // --- DECISION ROUTING (Tang 1 Output) ---
                var decision = new RouterDecisionDto
                {
                    IsValidSyntax = true,
                    Sloc = analysisResult.Sloc,
                    Vg = analysisResult.Vg
                };

                if (decision.Sloc < 25 || decision.Vg <= 2)
                {
                    decision.SelectedRoute = RoutingType.RawCode;
                    decision.Message = "Ham don gian, dinh tuyen su dung Ma Nguon Goc (Raw Code).";
                }
                else
                {
                    decision.SelectedRoute = RoutingType.HybridGraph;
                    decision.Message = "Ham phuc tap, dinh tuyen sang Tang 2: Ngu canh Lai (Hybrid Graph).";

                    // --- HANDOFF: Dong goi va chuyen tiep sang Tang 2 ---
                    var hybridInput = new HybridContextInputDto
                    {
                        ModuleId = request.ModuleId,
                        Language = request.Language.ToLower(),
                        RoutingDecision = "ROUTE_HYBRID",
                        Metrics = new HybridContextMetricsDto
                        {
                            Sloc = analysisResult.Sloc,
                            CyclomaticComplexity = analysisResult.Vg
                        },
                        RawSourceCode = request.SourceCode,
                        AstPayload = new AstPayloadDto
                        {
                            ParserType = "tree-sitter",
                            RootNodeType = analysisResult.RootNodeType,
                            HasError = analysisResult.HasError
                        }
                    };

                    decision.HybridContextResult = await _hybridContextGeneratorService.GenerateAsync(hybridInput);
                }

                return decision;
            }
            catch (Exception ex)
            {
                return new RouterDecisionDto
                {
                    IsValidSyntax = false,
                    Message = $"Co loi ngoai le xay ra trong qua trinh danh gia: {ex.Message}"
                };
            }
        }
    }
}