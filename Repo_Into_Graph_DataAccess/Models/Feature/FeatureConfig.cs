using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repo_Into_Graph_DataAccess.Models.Feature
{
    public class FeatureConfig
    {
        public string feature_name { get; set; } = string.Empty;
        public List<ApiConfig> apis { get; set; } = new();
    }
    public class ApiConfig
    {
        public string controller { get; set; } = string.Empty;
        public string method { get; set; } = string.Empty;
    }
}




