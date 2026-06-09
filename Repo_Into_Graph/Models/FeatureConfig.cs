using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph.Repo_Into_Graph.Models
{
    public class FeatureConfig
    {
        public string feature_name { get; set; } = string.Empty;
        public List<string> apis { get; set; } = new();
    }
}
