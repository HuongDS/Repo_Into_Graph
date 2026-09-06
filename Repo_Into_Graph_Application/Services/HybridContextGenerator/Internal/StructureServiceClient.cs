using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator.Internal
{
    /// <summary>
    /// Goi endpoint /api/parse-structure cua Python Microservice de lay cay cau lenh
    /// do TREE-SITTER phan tich, roi chuyen ve dung mo hinh MethodDecl / Stmt.
    ///
    /// Neu service khong san sang (chua bat, timeout, loi) thi tra ve null - Tang 2
    /// tu dong quay ve bo parser noi bo (CodeStructureParser) nen khong bao gio chet.
    /// </summary>
    internal sealed class StructureServiceClient
    {
        private const int TimeoutSeconds = 8;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public StructureServiceClient(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient;
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
        }

        /// <summary>
        /// Tra ve danh sach ham da phan tich, hoac null neu khong dung duoc ket qua.
        /// </summary>
        public async Task<StructureResult?> TryParseAsync(string code, string language)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(code)) return null;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

                var response = await _httpClient.PostAsJsonAsync(
                    _baseUrl + "/api/parse-structure",
                    new { code, language },
                    cts.Token);

                if (!response.IsSuccessStatusCode) return null;

                var payload = await response.Content.ReadFromJsonAsync<StructureResponse>(JsonOptions, cts.Token);
                if (payload == null || !payload.Ok || payload.Methods == null || payload.Methods.Count == 0)
                {
                    return null;
                }

                var methods = new List<MethodDecl>();
                foreach (var dto in payload.Methods)
                {
                    methods.Add(ToMethodDecl(dto));
                }

                return new StructureResult
                {
                    Methods = methods,
                    Parser = string.IsNullOrEmpty(payload.Parser) ? "tree-sitter" : payload.Parser,
                    HasError = payload.HasError,
                    Warnings = payload.Warnings ?? new List<string>()
                };
            }
            catch (Exception)
            {
                // Moi loi (khong ket noi duoc, timeout, JSON sai dinh dang...) -> dung parser noi bo
                return null;
            }
        }

        // -- Chuyen DTO -> mo hinh noi bo -----------------------------
        private static MethodDecl ToMethodDecl(MethodDto dto)
        {
            return new MethodDecl
            {
                Name = dto.Name ?? string.Empty,
                Parameters = dto.Parameters ?? string.Empty,
                ReturnType = dto.ReturnType ?? string.Empty,
                Signature = dto.Signature ?? string.Empty,
                Modifiers = dto.Modifiers ?? new List<string>(),
                Annotations = dto.Annotations ?? new List<string>(),
                ThrowsTypes = dto.ThrowsTypes ?? new List<string>(),
                StartLine = dto.StartLine,
                IsAsync = dto.IsAsync,
                IsStatic = dto.IsStatic,
                IsSynthetic = string.Equals(dto.Signature, "(code block)", StringComparison.Ordinal),
                Body = ToStmtList(dto.Body)
            };
        }

        private static List<Stmt> ToStmtList(List<StmtDto>? items)
        {
            var result = new List<Stmt>();
            if (items == null) return result;

            foreach (var item in items)
            {
                var stmt = ToStmt(item);
                if (stmt != null) result.Add(stmt);
            }
            return result;
        }

        private static Stmt? ToStmt(StmtDto? dto)
        {
            if (dto == null) return null;

            if (!Enum.TryParse<StmtKind>(dto.Kind, ignoreCase: true, out var kind))
            {
                kind = StmtKind.Simple;
            }

            var stmt = new Stmt
            {
                Kind = kind,
                Start = dto.Start,
                End = dto.End,
                Line = dto.Line,
                Text = dto.Text ?? string.Empty,
                HeaderText = dto.HeaderText ?? string.Empty,
                Condition = dto.Condition ?? string.Empty,
                ForInit = dto.ForInit ?? string.Empty,
                ForUpdate = dto.ForUpdate ?? string.Empty,
                Body = ToStmtList(dto.Body),
                Else = ToStmtList(dto.Else),
                Finally = ToStmtList(dto.Finally)
            };

            if (dto.Catches != null)
            {
                foreach (var clause in dto.Catches)
                {
                    stmt.Catches.Add(new CatchClauseInfo
                    {
                        Start = clause.Start,
                        Line = clause.Line,
                        Parameter = clause.Parameter ?? string.Empty,
                        ExceptionType = string.IsNullOrEmpty(clause.ExceptionType) ? "Exception" : clause.ExceptionType,
                        HeaderText = clause.HeaderText ?? "catch",
                        Body = ToStmtList(clause.Body)
                    });
                }
            }

            if (dto.Cases != null)
            {
                foreach (var branch in dto.Cases)
                {
                    stmt.Cases.Add(new SwitchCaseInfo
                    {
                        Label = branch.Label ?? string.Empty,
                        IsDefault = branch.IsDefault,
                        Line = branch.Line,
                        Body = ToStmtList(branch.Body)
                    });
                }
            }

            return stmt;
        }

        // -- DTO khop voi JSON tra ve tu Python -----------------------
        internal sealed class StructureResult
        {
            public List<MethodDecl> Methods { get; set; } = new();
            public string Parser { get; set; } = "tree-sitter";
            public bool HasError { get; set; }
            public List<string> Warnings { get; set; } = new();
        }

        private sealed class StructureResponse
        {
            public bool Ok { get; set; }
            public string? Parser { get; set; }
            public string? Language { get; set; }
            public bool HasError { get; set; }
            public List<MethodDto>? Methods { get; set; }
            public List<string>? Warnings { get; set; }
            public string? Error { get; set; }
        }

        private sealed class MethodDto
        {
            public string? Name { get; set; }
            public string? Parameters { get; set; }
            public string? ReturnType { get; set; }
            public string? Signature { get; set; }
            public List<string>? Modifiers { get; set; }
            public List<string>? Annotations { get; set; }
            public List<string>? ThrowsTypes { get; set; }
            public int StartLine { get; set; }
            public bool IsAsync { get; set; }
            public bool IsStatic { get; set; }
            public List<StmtDto>? Body { get; set; }
        }

        private sealed class StmtDto
        {
            public string? Kind { get; set; }
            public int Start { get; set; }
            public int End { get; set; }
            public int Line { get; set; }
            public string? Text { get; set; }
            public string? HeaderText { get; set; }
            public string? Condition { get; set; }
            public string? ForInit { get; set; }
            public string? ForUpdate { get; set; }
            public List<StmtDto>? Body { get; set; }

            [JsonPropertyName("else")]
            public List<StmtDto>? Else { get; set; }

            [JsonPropertyName("finally")]
            public List<StmtDto>? Finally { get; set; }

            public List<CatchDto>? Catches { get; set; }
            public List<CaseDto>? Cases { get; set; }
        }

        private sealed class CatchDto
        {
            public int Start { get; set; }
            public int Line { get; set; }
            public string? Parameter { get; set; }
            public string? ExceptionType { get; set; }
            public string? HeaderText { get; set; }
            public List<StmtDto>? Body { get; set; }
        }

        private sealed class CaseDto
        {
            public string? Label { get; set; }
            public bool IsDefault { get; set; }
            public int Line { get; set; }
            public List<StmtDto>? Body { get; set; }
        }
    }
}
