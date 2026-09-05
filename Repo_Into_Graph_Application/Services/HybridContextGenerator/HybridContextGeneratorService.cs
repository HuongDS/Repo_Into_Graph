using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;
using Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator
{
    /// <summary>
    /// TANG 2 - HYBRID CONTEXT GENERATOR.
    ///
    /// Nhan input da chuan hoa tu Tang 1 (Adaptive Context Router) va sinh ra "Ngu canh Lai":
    ///   1. CFG Skeleton  : khung luong dieu khien dang Mermaid (thay cho toan bo raw code).
    ///   2. Enriched Metadata : async/await markers, annotation tags, dependency info,
    ///                          exception, thong ke luong dieu khien, tag ngu nghia.
    ///   3. Critical Snippets : cac doan code BAT BUOC giu nguyen van
    ///                          (nhanh if, throw, catch, vong lap phuc tap, loi goi async...).
    ///   4. HybridPrompt  : ban lap rap san de nap thang vao prompt cua Tang 3.
    ///
    /// Toan bo qua trinh chay noi bo trong .NET (khong phu thuoc Python Microservice)
    /// nen khong lam thay doi bat ky hanh vi nao cua Tang 1.
    /// </summary>
    public class HybridContextGeneratorService : IHybridContextGeneratorService
    {
        /// <summary>Gioi han do dai ma nguon dau vao de tranh treo API.</summary>
        private const int MaxSourceLength = 200_000;

        private readonly ILogger<HybridContextGeneratorService> _logger;
        private readonly StructureServiceClient _structureClient;
        private readonly bool _useTreeSitter;

        public HybridContextGeneratorService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<HybridContextGeneratorService> logger)
        {
            _logger = logger;

            string pythonUrl = configuration["PythonMicroserviceUrl"] ?? "http://localhost:8000";
            _useTreeSitter = !string.Equals(configuration["HybridContextUseTreeSitter"], "false",
                                            StringComparison.OrdinalIgnoreCase);
            _structureClient = new StructureServiceClient(httpClient, pythonUrl);
        }

        public async Task<HybridContextOutputDto> GenerateAsync(HybridContextInputDto input)
        {
            return await GenerateInternalAsync(input);
        }

        private async Task<HybridContextOutputDto> GenerateInternalAsync(HybridContextInputDto input)
        {
            var stopwatch = Stopwatch.StartNew();

            if (input == null)
            {
                return new HybridContextOutputDto
                {
                    Status = "FAILED",
                    Message = "Input rong (null) - khong the sinh ngu canh lai."
                };
            }

            var output = new HybridContextOutputDto
            {
                ModuleId = input.ModuleId ?? string.Empty,
                Language = NormalizeLanguage(input.Language),
                RouteDecision = string.IsNullOrWhiteSpace(input.RoutingDecision)
                    ? "ROUTE_HYBRID"
                    : input.RoutingDecision,
                GeneratedAtUtc = DateTime.UtcNow
            };

            // Co bi giam chat luong phan tich hay khong (chi canh bao thong tin thi khong tinh)
            bool degraded = false;

            try
            {
                string rawCode = input.RawSourceCode ?? string.Empty;

                if (string.IsNullOrWhiteSpace(rawCode))
                {
                    output.Status = "FAILED";
                    output.Message = "RawSourceCode rong - Tang 2 khong co du lieu de xay dung CFG.";
                    output.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                    return output;
                }

                if (rawCode.Length > MaxSourceLength)
                {
                    rawCode = rawCode.Substring(0, MaxSourceLength);
                    output.Warnings.Add($"Ma nguon vuot qua {MaxSourceLength} ky tu nen da bi cat bot.");
                    degraded = true;
                }

                if (!string.Equals(input.RoutingDecision, "ROUTE_HYBRID", StringComparison.OrdinalIgnoreCase))
                {
                    output.Warnings.Add(
                        $"RoutingDecision = '{input.RoutingDecision}' (khong phai ROUTE_HYBRID) - " +
                        "van xu ly de phuc vu kiem thu / so sanh benchmark.");
                }

                if (input.AstPayload != null && input.AstPayload.HasError)
                {
                    output.Warnings.Add("Tang 1 bao AST co loi cu phap - CFG duoc dung theo che do best-effort.");
                    degraded = true;
                }

                if (!IsSupportedLanguage(input.Language))
                {
                    output.Warnings.Add(
                        $"Ngon ngu '{input.Language}' khong nam trong danh sach ho tro (java, csharp) - " +
                        "su dung bo phan tich mac dinh kieu C-family.");
                    degraded = true;
                }

                // -- 1. Phan tich cau truc ma nguon --------------------------
                // Uu tien tree-sitter (qua Python Microservice, cung bo parser voi Tang 1).
                // Service khong san sang -> tu dong quay ve parser noi bo, khong bao gio chet.
                var scanner = new SourceScanner(rawCode);
                List<MethodDecl> methods;

                var remote = _useTreeSitter
                    ? await _structureClient.TryParseAsync(rawCode, output.Language)
                    : null;

                if (remote != null && remote.Methods.Count > 0)
                {
                    methods = remote.Methods;
                    output.Parser = remote.Parser;

                    foreach (var warning in remote.Warnings)
                    {
                        output.Warnings.Add(warning);
                    }

                    if (remote.HasError)
                    {
                        output.Warnings.Add("tree-sitter bao cay cu phap co node loi - CFG la best-effort.");
                        degraded = true;
                    }
                }
                else
                {
                    output.Parser = "builtin";
                    if (_useTreeSitter)
                    {
                        output.Warnings.Add(
                            "Khong goi duoc /api/parse-structure (Python Microservice) - " +
                            "da dung bo parser noi bo cua .NET.");
                    }

                    var parser = new CodeStructureParser(scanner);
                    methods = parser.ExtractMethods();

                    if (methods.Count == 0)
                    {
                        // Ma nguon chi la mot doan lenh (khong co khai bao ham) -> tao ham gia lap
                        methods.Add(new MethodDecl
                        {
                            Name = string.IsNullOrWhiteSpace(input.ModuleId) ? "code" : input.ModuleId,
                            Signature = "(code block)",
                            BodyStart = 0,
                            BodyEnd = rawCode.Length,
                            StartLine = 1,
                            IsSynthetic = true
                        });
                        output.Warnings.Add("Khong tim thay khai bao ham - CFG duoc dung tren toan bo doan ma nguon.");
                        degraded = true;
                    }

                    foreach (var method in methods)
                    {
                        method.Body = parser.ParseStatements(method.BodyStart, method.BodyEnd);
                    }
                }

                // -- 2. Dung CFG skeleton ------------------------------------
                // Neu ModuleId chi dich danh mot ham (vd "AppointmentService.book") thi chi
                // dung CFG cho ham do - phan con lai cua file van duoc dua vao metadata.
                var focusMethods = SelectFocusMethods(methods, input.ModuleId);
                if (focusMethods.Count < methods.Count)
                {
                    output.Warnings.Add(
                        $"ModuleId tro toi ham '{focusMethods[0].Name}' - CFG chi dung cho ham nay " +
                        $"({methods.Count - focusMethods.Count} ham khac chi nam trong metadata).");
                }

                var builder = new CfgBuilder();
                foreach (var method in focusMethods)
                {
                    builder.BuildMethod(method);
                }

                if (builder.Truncated)
                {
                    output.Warnings.Add("CFG qua lon nen da duoc rut gon (gioi han so node).");
                    degraded = true;
                }

                output.CfgNodes = builder.Nodes.ToList();
                output.CfgEdges = builder.Edges.ToList();
                output.CfgSkeleton = MermaidRenderer.Render(output.CfgNodes, output.CfgEdges);

                // -- 3. Trich xuat Critical Snippets -------------------------
                var snippetExtractor = new CriticalSnippetExtractor();
                var snippets = snippetExtractor.Extract(focusMethods, builder.StmtNodeMap);

                output.CriticalSnippetDetails = snippets;
                output.CriticalSnippets = snippets.Select(s => s.Code).ToList();

                // -- 4. Gan Enriched Metadata --
                var stats = builder.Stats;
                stats.HasComplexLoop = snippetExtractor.HasComplexLoop;

                var metadataExtractor = new EnrichedMetadataExtractor(scanner, methods);
                output.EnrichedMetadata = metadataExtractor.Extract(stats);

                // -- 5. Lap rap ngu canh lai cho Tang 3 --
                output.HybridPrompt = HybridPromptComposer.Compose(
                    output.CfgSkeleton, output.EnrichedMetadata, snippets);

                // -- 6. Chi so do luong + muc tiet kiem token --
                int originalTokens = EstimateTokens(rawCode);
                int hybridTokens = EstimateTokens(output.HybridPrompt);

                output.Metrics = new HybridMetricsDto
                {
                    OriginalSloc = input.Metrics?.Sloc ?? 0,
                    CyclomaticComplexity = input.Metrics?.CyclomaticComplexity ?? 0,
                    EstimatedTokenSavingPct = originalTokens == 0
                        ? 0
                        : Math.Round((1.0 - (double)hybridTokens / originalTokens) * 100.0, 1),
                    CyclomaticComplexityFromCfg = Math.Max(
                        1, output.CfgEdges.Count - output.CfgNodes.Count + 2 * Math.Max(1, focusMethods.Count)),
                    OriginalTokens = originalTokens,
                    HybridTokens = hybridTokens,
                    CfgNodeCount = output.CfgNodes.Count,
                    CfgEdgeCount = output.CfgEdges.Count,
                    MethodCount = focusMethods.Count,
                    CriticalSnippetCount = snippets.Count
                };

                output.Status = degraded ? "PARTIAL" : "SUCCESS";
                output.Message =
                    $"Da sinh ngu canh lai cho module '{output.ModuleId}': " +
                    $"{output.CfgNodes.Count} node / {output.CfgEdges.Count} canh CFG, " +
                    $"{output.CriticalSnippets.Count} critical snippet, " +
                    $"{output.EnrichedMetadata.Dependencies.Count} phu thuoc, " +
                    $"V(G) tu CFG = {output.Metrics.CyclomaticComplexityFromCfg}, " +
                    $"tiet kiem ~{output.Metrics.EstimatedTokenSavingPct}% token.";

                stopwatch.Stop();
                output.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                _logger?.LogInformation(
                    "[Tang 2] Module '{ModuleId}' | {Language} | Parser: {Parser} | Status: {Status} | " +
                    "Nodes: {Nodes} | Edges: {Edges} | Snippets: {Snippets} | {Elapsed} ms",
                    output.ModuleId, output.Language, output.Parser, output.Status,
                    output.CfgNodes.Count, output.CfgEdges.Count,
                    output.CriticalSnippets.Count, output.ProcessingTimeMs);

                return output;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger?.LogError(ex, "[Tang 2] Loi khi sinh ngu canh lai cho module '{ModuleId}'", output.ModuleId);

                output.Status = "FAILED";
                output.Message = "Loi khi sinh ngu canh lai: " + ex.Message;
                output.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return output;
            }
        }

        /// <summary>
        /// Neu ModuleId chi dich danh mot ham (phan sau dau '.') va ten do khop voi
        /// mot ham trong ma nguon thi chi lay ham do de dung CFG.
        /// Nho vay khi Orchestrator gui ca file/class, ngu canh lai chi tap trung vao
        /// ham can kiem thu -> tiet kiem token that su.
        /// </summary>
        private static List<MethodDecl> SelectFocusMethods(List<MethodDecl> methods, string? moduleId)
        {
            if (methods.Count <= 1 || string.IsNullOrWhiteSpace(moduleId)) return methods;

            string name = moduleId.Trim();
            int dot = name.LastIndexOf('.');
            if (dot >= 0) name = name.Substring(dot + 1);

            int paren = name.IndexOf('(');
            if (paren >= 0) name = name.Substring(0, paren);
            name = name.Trim();
            if (name.Length == 0) return methods;

            var matched = methods
                .Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return matched.Count > 0 ? matched : methods;
        }

        /// <summary>
        /// Uoc luong so token theo quy uoc pho bien (~4 ky tu / token) de tinh
        /// muc tiet kiem giua raw source code va hybrid prompt.
        /// </summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private static string NormalizeLanguage(string? language)
        {
            string value = (language ?? string.Empty).Trim().ToLowerInvariant();
            return value switch
            {
                "java" => "java",
                "csharp" or "c#" or "cs" or "dotnet" or "net" => "csharp",
                _ => string.IsNullOrEmpty(value) ? "csharp" : value
            };
        }

        private static bool IsSupportedLanguage(string? language)
        {
            string value = NormalizeLanguage(language);
            return value == "java" || value == "csharp";
        }
    }
}
