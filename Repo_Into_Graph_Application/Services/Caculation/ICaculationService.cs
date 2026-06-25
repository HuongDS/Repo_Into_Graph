using Repo_Into_Graph_Application.Dtos.QuestionGenerate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Services.Caculation
{
    public interface ICaculationService
    {
        public Task<double> CalculateCodeCoverage(IEnumerable<GeneratedQuestionDto> generatedQuestions, Guid businessID);
    }
}
