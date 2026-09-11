using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.LLMOrchestrator;
using Repo_Into_Graph_Application.Dtos.QuestionGenerate;

namespace Repo_Into_Graph_Application.Services.LLMOrchestrator
{
    public interface IE2EOrchestratorService
    {
        Task<GenerateQuestionsResponse> GenerateE2EQuestionsAsync(GenerateQuestionsRequest request);
    }
}
