using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.StudentAnswerEvaluation;

namespace Repo_Into_Graph_Application.Services.StudentAnswerEvaluation
{
    public interface IStudentEvaluationService
    {
        Task<string> EvaluateStudentAnswerAsync(StudentEvaluationRequestDto request);
    }
}
