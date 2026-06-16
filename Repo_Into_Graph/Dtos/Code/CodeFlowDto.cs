using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Dtos.Method;

namespace Repo_Into_Graph.Repo_Into_Graph.Dtos.Code
{
    public class CodeFlowDto
    {
        public FeatureViewDto Feature { get; set; } = new();
        public List<MethodSourceDto> Methods { get; set; } = new();
    }
}
