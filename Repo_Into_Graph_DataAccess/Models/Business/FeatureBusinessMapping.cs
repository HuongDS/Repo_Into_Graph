using System;

namespace Repo_Into_Graph_DataAccess.Models.Business
{
    /// <summary>
    /// Bảng trung gian nhiều-nhiều giữa Feature (luồng phân tích) và Business (nhóm chức năng).
    /// </summary>
    public class FeatureBusinessMapping
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
        public Guid BusinessId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public Feature.Feature? Feature { get; set; }
        public Business? Business { get; set; }
    }
}
