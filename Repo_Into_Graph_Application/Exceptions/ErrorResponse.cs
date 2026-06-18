using System;
using System.Collections.Generic;

namespace Repo_Into_Graph_Application.Exceptions
{
    /// <summary>
    /// Cấu trúc JSON thống nhất cho mọi error response trả về client.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>HTTP status code.</summary>
        public int StatusCode { get; set; }

        /// <summary>Thông điệp lỗi ngắn gọn.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Loại exception (chỉ hiển thị khi môi trường development).</summary>
        public string? ExceptionType { get; set; }

        /// <summary>Stack trace (chỉ hiển thị khi môi trường development).</summary>
        public string? StackTrace { get; set; }

        /// <summary>Thời điểm xảy ra lỗi (UTC).</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Validation errors (nếu có).</summary>
        public IDictionary<string, string[]>? ValidationErrors { get; set; }
    }
}


