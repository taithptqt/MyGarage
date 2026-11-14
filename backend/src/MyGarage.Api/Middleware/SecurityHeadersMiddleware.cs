namespace MyGarage.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (!_environment.IsDevelopment())
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments("/api") || path.StartsWithSegments("/odata"))
                {
                    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
                }
                else
                {
                    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'";
                }
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
