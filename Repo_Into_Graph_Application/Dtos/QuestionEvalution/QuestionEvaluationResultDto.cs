using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Dtos.QuestionEvalution
{
    public class QuestionEvaluationResultDto
    {
        public string Question { get; set; }
        public string SuggestedAnswer { get; set; }
        public EvaluationScores Scores { get; set; }
        public EvaluationDetails EvaluationDetails { get; set; }
    }
}
