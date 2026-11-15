using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MyGarage.Application.Interfaces.Common;

namespace MyGarage.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IPersistenceExceptionClassifier _persistenceExceptionClassifier;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment,
        IPersistenceExceptionClassifier persistenceExceptionClassifier)
    {
        _logger = logger;
        _environment = environment;
        _persistenceExceptionClassifier = persistenceExceptionClassifier;
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

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = _environment.IsDevelopment() ? exception.Message : null,
            Type = GetProblemType(statusCode),
            Instance = sanitizedPath,
            Extensions =
            {
                ["message"] = message,
                ["errorCode"] = errorCode,
                ["statusCode"] = statusCode,
                ["traceId"] = httpContext.TraceIdentifier
            }
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private (int StatusCode, string ErrorCode, string Message) MapException(Exception exception)
    {
        var dbEx = FindDbException(exception);
        if (dbEx != null)
        {
            var msg = dbEx.Message;
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

            var sqlErrorNumber = dbEx.GetType().GetProperty("Number")?.GetValue(dbEx) as int?;
            if (sqlErrorNumber == -2 || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return ((int)HttpStatusCode.GatewayTimeout, "DATABASE_TIMEOUT",
                    "Hết thời gian chờ phản hồi từ cơ sở dữ liệu.");
            }

            return ((int)HttpStatusCode.InternalServerError, "DATABASE_ERROR",
                _environment.IsDevelopment()
                    ? $"Lỗi cơ sở dữ liệu ({sqlErrorNumber ?? dbEx.ErrorCode}): {dbEx.Message}"
                    : "Đã xảy ra lỗi truy cập dữ liệu.");
        }

        var persistenceClassification = _persistenceExceptionClassifier.Classify(exception);
        if (persistenceClassification.Kind == PersistenceExceptionKind.ConcurrencyConflict)
        {
            return ((int)HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT",
                "Xung đột dữ liệu đồng thời. Bản ghi đã được cập nhật bởi phiên khác, vui lòng tải lại và thử lại.");
        }

        if (persistenceClassification.Kind == PersistenceExceptionKind.UpdateError)
        {
            return ((int)HttpStatusCode.BadRequest, "DATABASE_UPDATE_ERROR",
                _environment.IsDevelopment()
                    ? $"Lỗi ràng buộc dữ liệu: {persistenceClassification.DetailMessage}"
                    : "Dữ liệu không hợp lệ hoặc vi phạm ràng buộc hệ thống.");
        }

        return exception switch
        {
            UnauthorizedAccessException unauthorizedEx => ((int)HttpStatusCode.Unauthorized, "UNAUTHORIZED",
                _environment.IsDevelopment() ? unauthorizedEx.Message : "Không được phép truy cập."),
            ArgumentException or InvalidOperationException => ((int)HttpStatusCode.BadRequest, "INVALID_OPERATION",
                _environment.IsDevelopment() ? exception.Message : "Yêu cầu không hợp lệ hoặc không thể thực hiện."),
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

    private static string GetProblemType(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        500 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
        504 => "https://tools.ietf.org/html/rfc9110#section-15.6.5",
        _ => $"https://httpstatuses.io/{statusCode}"
    };

    private static DbException? FindDbException(Exception? ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is DbException dbEx)
            {
                return dbEx;
            }
            current = current.InnerException;
        }

        return null;
    }
}
