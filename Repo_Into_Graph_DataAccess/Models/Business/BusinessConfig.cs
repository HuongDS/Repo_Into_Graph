using System.Collections.Generic;

namespace Repo_Into_Graph_DataAccess.Models.Business
{
    /// <summary>
    /// Config đọc từ template_business.json — mỗi entry là một Business (nhóm chức năng).
    /// </summary>
    public class BusinessConfig
    {
        public string business_name { get; set; } = string.Empty;
        public List<ApiConfig> apis { get; set; } = new();
    }

    public class ApiConfig
    {
        public string controller { get; set; } = string.Empty;
        public string method { get; set; } = string.Empty;
    }
}
