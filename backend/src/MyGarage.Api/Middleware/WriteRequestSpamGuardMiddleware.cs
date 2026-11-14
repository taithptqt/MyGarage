using System.Security.Cryptography;
using System.Security.Claims;
using MyGarage.Interfaces;

namespace MyGarage.Middleware;

public sealed class WriteRequestSpamGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWriteRequestSpamGuard _guard;

    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public WriteRequestSpamGuardMiddleware(RequestDelegate next, IWriteRequestSpamGuard guard)
    {
        _next = next;
        _guard = guard;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!ShouldGuard(context.Request))
        {
            await _next(context);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var key = await BuildRequestKeyAsync(context.Request, context.User, context.Connection.RemoteIpAddress?.ToString(), context.RequestAborted);

        if (_guard.IsCoolingDown(key, nowUtc))
        {
            await RejectAsync(context, "Thao tac trung lap qua nhanh. Vui long thu lai sau.");
            return;
        }

        if (!_guard.TryEnter(key, nowUtc, out _))
        {
            await RejectAsync(context, "Yeu cau dang duoc xu ly. Vui long cho.");
            return;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            _guard.Exit(key, DateTime.UtcNow);
        }
    }

    private static bool ShouldGuard(HttpRequest request)
    {
        if (!UnsafeMethods.Contains(request.Method))
        {
            return false;
        }

        if (request.Path.StartsWithSegments("/hubs"))
        {
            return false;
        }

        return true;
    }

    private static async Task<string> BuildRequestKeyAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        string? remoteIp,
        CancellationToken cancellationToken)
    {
        var actor =
            user.FindFirst("sub")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            user.Identity?.Name ??
            remoteIp ??
            "anonymous";

        var path = request.Path.ToString().ToLowerInvariant();
        var query = request.QueryString.HasValue ? request.QueryString.Value! : string.Empty;
        var bodyHash = await ComputeBodyHashAsync(request, cancellationToken);
        return $"{request.Method}:{path}{query}:{actor}:{bodyHash}";
    }

    private static async Task<string> ComputeBodyHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is null || request.ContentLength <= 0)
        {
            return "no-body";
        }

        if (request.ContentLength > 1024 * 1024)
        {
            return $"body-len:{request.ContentLength.Value}";
        }

        if (request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            return $"multipart:{request.ContentLength.Value}";
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken);
        request.Body.Position = 0;

        var bytes = memory.ToArray();
        if (bytes.Length == 0)
        {
            return "empty";
        }

        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"message\":\"{message}\"}}");
    }
}
