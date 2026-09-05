using Repo_Into_Graph_Application.Dtos.HybridContextGenerator;

namespace Repo_Into_Graph_Application.Services.HybridContextGenerator
{
    /// <summary>
    /// Contract (interface) cho Tang 2 - Hybrid Context Generator.
    /// Nhan input da duoc chuan hoa tu Tang 1 va tao ra nguyen lieu nghe context lai.
    /// </summary>
    public interface IHybridContextGeneratorService
    {
        /// <summary>
        /// Tao nguyen lieu context lai (CFG + Source) tu ma nguon phuc tap.
        /// </summary>
        /// <param name="input">Input da duoc dong goi boi Orchestrator Tang 1</param>
        /// <returns>Ket qua context lai (se duoc mo rong o Tang 2)</returns>
        Task<HybridContextOutputDto> GenerateAsync(HybridContextInputDto input);
    }
}
