using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Repo_Into_Graph_Application.Exceptions;

namespace Repo_Into_Graph_API.Exceptions
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _env;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Map exception sang status code và message
            var (statusCode, message) = MapException(exception);

            // Log theo severity
            if (statusCode >= 500)
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
            else
                _logger.LogWarning(exception, "Domain exception [{Status}]: {Message}", statusCode, exception.Message);

            // Build response
            var response = new ErrorResponse
            {
                StatusCode = statusCode,
                Error = message,
                Timestamp = DateTime.UtcNow
            };

            // Chỉ expose details khi môi trường Development
            if (_env.IsDevelopment())
            {
                response.ExceptionType = exception.GetType().Name;
                response.StackTrace = exception.StackTrace;
            }

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(response, _jsonOptions),
                cancellationToken);

            return true;
        }

        // ─── Exception → (statusCode, message) ───────────────────────────────────

        private static (int statusCode, string message) MapException(Exception ex) => ex switch
        {
            // Domain exceptions (custom)
            AppException app => (app.StatusCode, app.Message),

            // Framework / BCL exceptions được map thành domain code
            ArgumentNullException => (400, ex.Message),
            ArgumentOutOfRangeException => (400, ex.Message),
            ArgumentException => (400, ex.Message),
            ValidationException => (400, ex.Message),
            KeyNotFoundException => (404, ex.Message),
            InvalidOperationException => (422, ex.Message),
            UnauthorizedAccessException => (401, ex.Message),
            NotSupportedException => (400, ex.Message),
            TimeoutException => (504, "Yêu cầu bị timeout. Vui lòng thử lại sau."),
            OperationCanceledException => (499, "Yêu cầu bị hủy bởi client."),

            // Mọi exception khác → 500
            _ => (500, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
        };
    }
}



