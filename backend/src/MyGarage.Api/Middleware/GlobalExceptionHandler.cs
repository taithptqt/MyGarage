using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var sanitizedPath = SanitizePathForLogging(httpContext.Request.Path.Value);
        _logger.LogError(exception,
            "[GLOBAL-EXCEPTION] Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            sanitizedPath);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var (statusCode, errorCode, message) = MapException(exception);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            statusCode,
            errorCode,
            message,
            detail = _environment.IsDevelopment() ? exception.Message : null,
            traceId = httpContext.TraceIdentifier
        }, cancellationToken);

        return true;
    }

    private (int StatusCode, string ErrorCode, string Message) MapException(Exception exception)
    {
        var sqlEx = FindSqlException(exception);
        if (sqlEx != null)
        {
            var msg = sqlEx.Message;
            if (msg.Contains("paused", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("free amount allowance", StringComparison.OrdinalIgnoreCase))
            {
                return ((int)HttpStatusCode.ServiceUnavailable, "DATABASE_PAUSED",
                    "Cơ sở dữ liệu đám mây đang tạm dừng (Paused). Vui lòng thử lại sau ít giây.");
            }

            if (msg.Contains("cannot open database", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("login failed", StringComparison.OrdinalIgnoreCase))
            {
                return ((int)HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE",
                    "Không thể kết nối tới cơ sở dữ liệu.");
            }

            if (sqlEx.Number == -2 || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return ((int)HttpStatusCode.GatewayTimeout, "DATABASE_TIMEOUT",
                    "Hết thời gian chờ phản hồi từ cơ sở dữ liệu.");
            }

            return ((int)HttpStatusCode.InternalServerError, "DATABASE_ERROR",
                _environment.IsDevelopment()
                    ? $"Lỗi cơ sở dữ liệu ({sqlEx.Number}): {sqlEx.Message}"
                    : "Đã xảy ra lỗi truy cập dữ liệu.");
        }

        return exception switch
        {
            DbUpdateConcurrencyException => ((int)HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT",
                "Xung đột dữ liệu đồng thời. Bản ghi đã được cập nhật bởi phiên khác, vui lòng tải lại và thử lại."),
            DbUpdateException dbEx => ((int)HttpStatusCode.BadRequest, "DATABASE_UPDATE_ERROR",
                _environment.IsDevelopment()
                    ? $"Lỗi ràng buộc dữ liệu: {dbEx.InnerException?.Message ?? dbEx.Message}"
                    : "Dữ liệu không hợp lệ hoặc vi phạm ràng buộc hệ thống."),
            UnauthorizedAccessException unauthorizedEx => ((int)HttpStatusCode.Unauthorized, "UNAUTHORIZED",
                unauthorizedEx.Message),
            ArgumentException or InvalidOperationException => ((int)HttpStatusCode.BadRequest, "INVALID_OPERATION",
                exception.Message),
            TimeoutException => ((int)HttpStatusCode.GatewayTimeout, "REQUEST_TIMEOUT",
                "Yêu cầu quá thời gian chờ. Vui lòng thử lại sau."),
            _ => ((int)HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR",
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
        };
    }

    public static string SanitizePathForLogging(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        const string publicPrefix = "/quotations/public/";
        var idx = path.IndexOf(publicPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + publicPrefix.Length;
            var slashIdx = path.IndexOf('/', start);
            if (slashIdx >= 0)
            {
                return string.Concat(path.AsSpan(0, start), "[REDACTED]", path.AsSpan(slashIdx));
            }
            return string.Concat(path.AsSpan(0, start), "[REDACTED]");
        }

        return path;
    }

    private static SqlException? FindSqlException(Exception? ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is SqlException sqlEx)
            {
                return sqlEx;
            }
            current = current.InnerException;
        }

        return null;
    }
}
