using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Dtos.QuestionEvalution
{
    public class EvaluationDetails
    {
        public string FactualCorrectnessReason { get; set; }
        public string RelevanceCompletenessReason { get; set; }
        public string TechnicalClarityReason { get; set; }
    }
}
