using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Dtos.Feature;
using Repo_Into_Graph_Application.Dtos.Method;

namespace Repo_Into_Graph_Application.Dtos.Code
{
    public class CodeFlowDto
    {
        public FeatureViewDto Feature { get; set; } = new();
        public List<MethodSourceDto> Methods { get; set; } = new();
    }
}





