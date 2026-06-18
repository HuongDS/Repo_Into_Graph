using System;

namespace Repo_Into_Graph_Application.Exceptions
{
    /// <summary>
    /// Nền tảng cho mọi domain exception trong hệ thống.
    /// Mang theo HTTP status code gợi ý để Global Handler map sang response.
    /// </summary>
    public abstract class AppException : Exception
    {
        public int StatusCode { get; }

        protected AppException(string message, int statusCode, Exception? inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
        }
    }

    // ─── 400 Bad Request ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tham số đầu vào không hợp lệ (thay thế ArgumentException trong domain).
    /// </summary>
    public class BadRequestException : AppException
    {
        public BadRequestException(string message, Exception? inner = null)
            : base(message, 400, inner) { }
    }

    // ─── 404 Not Found ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resource không tồn tại trong hệ thống.
    /// </summary>
    public class NotFoundException : AppException
    {
        public NotFoundException(string resource, object key)
            : base($"Không tìm thấy {resource} với định danh: {key}", 404) { }

        public NotFoundException(string message)
            : base(message, 404) { }
    }

    // ─── 409 Conflict ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Xung đột dữ liệu (ví dụ: duplicate).
    /// </summary>
    public class ConflictException : AppException
    {
        public ConflictException(string message, Exception? inner = null)
            : base(message, 409, inner) { }
    }

    // ─── 422 Unprocessable ────────────────────────────────────────────────────────

    /// <summary>
    /// Request hợp lệ về cú pháp nhưng không thể xử lý (logic lỗi).
    /// </summary>
    public class UnprocessableException : AppException
    {
        public UnprocessableException(string message, Exception? inner = null)
            : base(message, 422, inner) { }
    }

    // ─── 502 External Service Error ───────────────────────────────────────────────

    /// <summary>
    /// Lỗi khi gọi dịch vụ bên ngoài (AI API, Git clone, ...).
    /// </summary>
    public class ExternalServiceException : AppException
    {
        public string ServiceName { get; }

        public ExternalServiceException(string serviceName, string message, Exception? inner = null)
            : base($"[{serviceName}] {message}", 502, inner)
        {
            ServiceName = serviceName;
        }
    }
}


