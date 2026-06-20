using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Dtos.QuestionEvalution
{
    public class EvaluationScores
    {
        public double FactualCorrectness { get; set; }
        public double RelevanceCompleteness { get; set; }
        public double TechnicalClarity { get; set; }
        public double AverageTotalScore { get; set; }
    }
}
