using System;

namespace Repo_Into_Graph_DataAccess.Models.FewShot
{
    public class FewShotExample
    {
        public Guid Id { get; set; }

        /// <summary>Câu hỏi mẫu của giảng viên</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>Đáp án gợi ý mẫu</summary>
        public string SuggestedAnswer { get; set; } = string.Empty;

        /// <summary>Mức độ khó: Easy / Medium / Hard</summary>
        public string Difficulty { get; set; } = string.Empty;

        /// <summary>Nhãn phân loại tự do (ví dụ: luồng, điều kiện, nghiệp vụ)</summary>
        public string? Tag { get; set; }

        /// <summary>Ghi chú thêm của giảng viên</summary>
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}




