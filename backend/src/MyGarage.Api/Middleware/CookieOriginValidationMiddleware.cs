using System.Net;
using System.Text.Json;
using MyGarage.Security;

namespace MyGarage.Middleware;

public sealed class CookieOriginValidationMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options
    };

    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedOrigins;
    private readonly bool _allowLocalhostInDev;

    public CookieOriginValidationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _next = next;
        _allowedOrigins = ResolveConfiguredOrigins(configuration);
        _allowLocalhostInDev = environment.IsDevelopment() || environment.IsEnvironment("Testing");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var usesAuthCookie = context.Request.Cookies.ContainsKey(AuthCookie.Name);
        var hasExplicitBearer = context.Request.Headers.Authorization.ToString()
            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

        if (usesAuthCookie && !hasExplicitBearer && !SafeMethods.Contains(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin.ToString().Trim();
            if (string.IsNullOrWhiteSpace(origin) || !IsOriginAllowed(origin))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    statusCode = (int)HttpStatusCode.Forbidden,
                    message = "The request origin is not allowed."
                }));
                return;
            }
        }

        await _next(context);
    }

    private bool IsOriginAllowed(string origin)
    {
        if (_allowedOrigins.Contains(origin))
        {
            return true;
        }

        if (_allowLocalhostInDev &&
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static HashSet<string> ResolveConfiguredOrigins(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var envOrigins = configuration["CORS_ALLOWED_ORIGINS"];
        if (!string.IsNullOrWhiteSpace(envOrigins))
        {
            foreach (var item in envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                origins.Add(item.TrimEnd('/'));
            }
        }

        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? configuration["App:FrontendUrl"];
        if (!string.IsNullOrWhiteSpace(frontendBaseUrl) &&
            Uri.TryCreate(frontendBaseUrl.Trim(), UriKind.Absolute, out var frontendUri))
        {
            origins.Add($"{frontendUri.Scheme}://{frontendUri.Authority}");
        }

        return origins;
    }
}
