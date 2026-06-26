using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Dtos.QuestionGenerate
{
    public class GenerateQuestionFullRequest
    {
        public string Repo_path { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public int NumberOfQuestions { get; set; }
        public string Difficulty { get; set; } = string.Empty;
    }
}
