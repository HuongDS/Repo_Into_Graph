using System.Collections.Generic;
using Repo_Into_Graph_Application.Dtos.Business;
using Repo_Into_Graph_Application.Dtos.Method;

namespace Repo_Into_Graph_Application.Dtos.Code
{
    public class CodeFlowDto
    {
        public BusinessViewDto Business { get; set; } = new();
        public List<MethodSourceDto> Methods { get; set; } = new();
    }
}
