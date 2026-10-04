using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Dtos.StudentAnswerEvaluation
{
    public class StudentEvaluationRequestDto
    {
        public Guid BusinessId { get; set; }
        public string Question { get; set; }
        public string ReferenceMaterial { get; set; }
        public string StudentAnswer { get; set; }
        public string FeedbackFormat { get; set; } // sandwich, rubric, explainable
        public string ContextModel { get; set; } // raw, graph, hybrid
        public double MaxScore { get; set; } // thang điểm tối đa (10, 100, etc)
    }
}
