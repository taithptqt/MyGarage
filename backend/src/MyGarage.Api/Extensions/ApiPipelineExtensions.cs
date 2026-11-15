using Microsoft.Extensions.FileProviders;
using MyGarage.Application.Interfaces.Common;
using MyGarage.Hubs;
using MyGarage.Middleware;

namespace MyGarage.Extensions;

public static class ApiPipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app, IConfiguration configuration)
    {
        var frontendDistPath = ResolveFrontendDistPath(app.Environment.ContentRootPath);
        var hasFrontendBuild = Directory.Exists(frontendDistPath);
        PhysicalFileProvider? frontendFileProvider = hasFrontendBuild ? new PhysicalFileProvider(frontendDistPath) : null;

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();
        app.UseForwardedHeaders();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHsts();
        }

        if (ShouldUseHttpsRedirection(configuration, app.Environment))
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles(); // Serve files from wwwroot (uploads, etc.)

        if (hasFrontendBuild && frontendFileProvider is not null)
        {
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = frontendFileProvider
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = frontendFileProvider
            });
        }

        app.UseCors(ApiSecurityExtensions.FrontendCorsPolicy);
        app.UseMiddleware<CookieOriginValidationMiddleware>();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<WriteRequestSpamGuardMiddleware>();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");
        app.MapApiHealthChecks();

        if (hasFrontendBuild)
        {
            app.MapFallback(async context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Path.StartsWithSegments("/odata") ||
                    context.Request.Path.StartsWithSegments("/swagger"))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await context.Response.SendFileAsync(Path.Combine(frontendDistPath, "index.html"));
            });
        }

        return app;
    }

    private static void MapApiHealthChecks(this WebApplication app)
    {
        // Liveness & readiness endpoints — used by Docker healthcheck and load balancers.
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }))
           .AllowAnonymous()
           .ExcludeFromDescription(); // hide from Swagger

        app.MapGet("/api/health/live", () => Results.Ok(new { status = "live", utc = DateTime.UtcNow }))
           .AllowAnonymous()
           .ExcludeFromDescription();

        app.MapGet("/api/health/ready", async (IDatabaseReadinessCheck dbCheck, CancellationToken cancellationToken) =>
        {
            var canConnect = await dbCheck.CanConnectAsync(cancellationToken);
            return canConnect
                ? Results.Ok(new { status = "ready", database = "connected", utc = DateTime.UtcNow })
                : Results.Json(new { status = "unready", database = "disconnected", utc = DateTime.UtcNow },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        })
           .AllowAnonymous()
           .ExcludeFromDescription();
    }

    private static string ResolveFrontendDistPath(string contentRootPath)
    {
        var rootsToCheck = new[]
        {
            contentRootPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in rootsToCheck)
        {
            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "frontend", "dist");
                var indexFile = Path.Combine(candidate, "index.html");
                if (File.Exists(indexFile))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        return Path.Combine(contentRootPath, "frontend", "dist");
    }

    private static bool ShouldUseHttpsRedirection(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return true;
        }

        var urls = configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrWhiteSpace(urls) &&
            urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var httpsPorts = configuration["ASPNETCORE_HTTPS_PORTS"];
        if (!string.IsNullOrWhiteSpace(httpsPorts))
        {
            return true;
        }

        var httpsPort = configuration["HTTPS_PORT"] ?? configuration["ASPNETCORE_HTTPS_PORT"];
        if (!string.IsNullOrWhiteSpace(httpsPort))
        {
            return true;
        }

        var kestrelHttpsUrl = configuration["Kestrel:Endpoints:Https:Url"];
        if (!string.IsNullOrWhiteSpace(kestrelHttpsUrl))
        {
            return true;
        }

        return false;
    }
}
