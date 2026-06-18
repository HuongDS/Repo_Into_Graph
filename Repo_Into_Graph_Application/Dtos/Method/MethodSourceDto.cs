using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_Application.Dtos.Method
{
    public class MethodSourceDto
    {
        public Guid Id { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string SourceCode { get; set; } = string.Empty;
    }
}





