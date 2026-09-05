using Microsoft.Extensions.Logging;
using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator
{
    /// <summary>
    /// [STUB] Placeholder cho Tang 2 - Hybrid Context Generator.
    /// Logic thuc su se duoc implement khi co chi thi.
    /// Hien tai chi log input nhan duoc va tra ve trang thai PENDING.
    /// </summary>
    public class HybridContextGeneratorService : IHybridContextGeneratorService
    {
        private readonly ILogger<HybridContextGeneratorService> _logger;

        public HybridContextGeneratorService(ILogger<HybridContextGeneratorService> logger)
        {
            _logger = logger;
        }

        public Task<HybridContextOutputDto> GenerateAsync(HybridContextInputDto input)
        {
            _logger.LogInformation(
                "[Tang 2 STUB] Nhan duoc yeu cau xu ly module '{ModuleId}' | Language: {Language} | " +
                "SLOC: {Sloc} | V(G): {Vg} | RootNode: {RootNode}",
                input.ModuleId,
                input.Language,
                input.Metrics.Sloc,
                input.Metrics.CyclomaticComplexity,
                input.AstPayload.RootNodeType
            );

            // TODO: Implement Tang 2 logic:
            // 1. Goi Python Microservice de build CFG tu AST
            // 2. Ket hop CFG nodes + Raw Source Code thanh Hybrid Context
            // 3. Tra ve danh sach context nodes cho Tang 3 su dung

            var result = new HybridContextOutputDto
            {
                ModuleId = input.ModuleId,
                Status = "PENDING",
                Message = $"[Tang 2 chua duoc implement] Module '{input.ModuleId}' da duoc dinh tuyen sang ROUTE_HYBRID voi {input.Metrics.Sloc} SLOC va V(G)={input.Metrics.CyclomaticComplexity}."
            };

            return Task.FromResult(result);
        }
    }
}
