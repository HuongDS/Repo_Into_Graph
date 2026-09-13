using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_DataAccess.Models.Business
{
    /// <summary>
    /// Config đọc từ template_business.json — mỗi entry là một Business (nhóm chức năng).
    /// </summary>
    public class BusinessConfig
    {
        public string business_name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public List<ApiConfig> apis { get; set; } = new();
    }

    /// <summary>
    /// Một API trong template_business.json.
    ///
    /// Hỗ trợ HAI schema, vì các repo thực nghiệm đang dùng hai cách viết khác nhau:
    ///
    ///   Schema A — gộp verb và tên hàm vào một trường:
    ///     { "controller": "BudgetController", "method": "POST CreateBudget" }
    ///
    ///   Schema B — tách riêng (repo Java test-appJava dùng dạng này):
    ///     { "controller": "LocationController", "http_method": "GET",
    ///       "function": "getAllProvinces", "path": "/api/auth/location/getAll" }
    ///
    /// Trước đây lớp này chỉ khai báo `controller` + `method`. Với schema B, `method` luôn
    /// rỗng nên GraphMapperService bỏ qua TOÀN BỘ api (93/93 ở test-appJava) — không sinh
    /// được FeatureBusinessMapping lẫn FeatureMethodMapping nào, khiến mọi nghiệp vụ báo
    /// "Không tìm thấy Source Code" dù repo đã parse ra đủ method.
    /// </summary>
    public class ApiConfig
    {
        public string controller { get; set; } = string.Empty;

        /// <summary>Schema A: "POST CreateBudget" hoặc chỉ "CreateBudget".</summary>
        public string method { get; set; } = string.Empty;

        /// <summary>Schema B: verb HTTP tách riêng ("GET", "POST"...). Chỉ để tham khảo.</summary>
        public string http_method { get; set; } = string.Empty;

        /// <summary>Schema B: tên hàm thuần trong controller ("getAllProvinces").</summary>
        public string function { get; set; } = string.Empty;

        /// <summary>Schema B: route của endpoint. Chỉ để tham khảo.</summary>
        public string path { get; set; } = string.Empty;

        /// <summary>
        /// Trả về TÊN HÀM THUẦN, đọc được từ cả hai schema.
        /// Verb HTTP không bao giờ được lọt vào đây: khoá đối chiếu với call graph
        /// (GraphMapperService.BuildKey) chỉ gồm class + tên hàm.
        /// </summary>
        public string ResolveMethodName()
        {
            // Schema B được ưu tiên vì nó đã tách sẵn, không cần đoán.
            if (!string.IsNullOrWhiteSpace(function))
                return function.Trim();

            if (string.IsNullOrWhiteSpace(method))
                return string.Empty;

            // Schema A: "POST CreateBudget" -> "CreateBudget"; "CreateBudget" -> "CreateBudget".
            var parts = method.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
        }
    }
}
