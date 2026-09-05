using Repo_Into_Graph_Application.Dtos.AdaptiveContextRouter;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.AdaptiveContextRouter
{
    public interface IAdaptiveContextRouterService
    {
        Task<RouterDecisionDto> EvaluateCodeContextAsync(RouterRequestDto request);
    }
}
