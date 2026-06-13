using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Dtos
{
    public class CodeFlowDto
    {
        public FeatureViewDto Feature { get; set; } = new();
        public List<MethodSourceDto> Methods { get; set; } = new();
    }
}
