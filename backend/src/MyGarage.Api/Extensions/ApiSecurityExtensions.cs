using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MyGarage.Application.Authorization;
using MyGarage.Controllers;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Extensions;

public static class ApiSecurityExtensions
{
    public const string FrontendCorsPolicy = "FrontendCorsPolicy";
    public const string AdminStaffOnlyPolicy = "AdminStaffOnly";
    public const string StaffOnlyPolicy = "StaffOnly";
    public const string AuditLogsODataAccessPolicy = "AuditLogsODataAccess";

    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedCorsOrigins = ResolveAllowedCorsOrigins(configuration, environment);
        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                        IsAllowedCorsOrigin(origin, allowedCorsOrigins, environment))
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()
                      .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size", "X-Total-Pages",
                          "Link", "X-Salary-Pending-Count", "X-Salary-Approved-Count");
            });
        });

        return services;
    }

    public static IServiceCollection AddApiForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                if (!IPAddress.TryParse(proxy, out var address))
                    throw new InvalidOperationException($"Invalid trusted proxy address: {proxy}");
                options.KnownProxies.Add(address);
            }
        });

        return services;
    }

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be configured and at least 32 UTF-8 bytes long for HS256.");
        }

        var jwtIssuer = configuration["Jwt:Issuer"] ?? "MyGarageServer";
        var jwtAudience = configuration["Jwt:Audience"] ?? "MyGarageClients";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // An explicit bearer credential must take precedence over ambient cookies.
                    // Leave it to JwtBearerHandler to validate, including malformed tokens.
                    if (context.Request.Headers.Authorization.ToString()
                        .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.CompletedTask;
                    }

                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        path.StartsWithSegments("/hubs/chat"))
                    {
                        context.Token = accessToken;
                        return Task.CompletedTask;
                    }

                    if (string.IsNullOrWhiteSpace(context.Token) &&
                        context.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken) &&
                        !string.IsNullOrWhiteSpace(cookieToken))
                    {
                        context.Token = cookieToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    if (context.Principal == null)
                    {
                        context.Fail("Invalid session.");
                        return;
                    }

                    using var scope = context.HttpContext.RequestServices.CreateScope();
                    var blacklist = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();

                    var rawToken = context.SecurityToken switch
                    {
                        JwtSecurityToken jwt => jwt.RawData,
                        Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonJwt => jsonJwt.EncodedToken,
                        _ => null
                    };
                    if (string.IsNullOrWhiteSpace(rawToken) &&
                        context.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken))
                    {
                        rawToken = cookieToken;
                    }

                    if (await blacklist.IsTokenRevokedAsync(rawToken))
                    {
                        context.Fail("Session has been revoked.");
                        return;
                    }

                    var role = context.Principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                    var userIdRaw = context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var authTicksRaw = context.Principal.FindFirst("auth_time_ticks")?.Value;
                    DateTime? issuedAtUtc = long.TryParse(authTicksRaw, out var ticks)
                        ? new DateTime(ticks, DateTimeKind.Utc)
                        : context.SecurityToken?.ValidFrom;
                    if (long.TryParse(userIdRaw, out var userId) &&
                        blacklist.IsUserRevoked(role, userId, issuedAtUtc))
                    {
                        context.Fail("Session has been invalidated.");
                        return;
                    }

                    var validator = scope.ServiceProvider.GetRequiredService<IAuthSecurityStampValidator>();
                    var isValid = await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted);
                    if (!isValid)
                    {
                        context.Fail("Session is no longer valid.");
                    }
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(AdminStaffOnlyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
                    if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var roleCode = context.User.FindFirst("role_code")?.Value;
                    return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase);
                });
            });

            options.AddPolicy(StaffOnlyPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context => context.User.IsStaffUser());
            });

            options.AddPolicy(AuditLogsODataAccessPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.IsAdminStaff() || context.User.IsStaffWithAnyKeyword("MANAGER"));
            });
        });

        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            static string ResolvePartitionKey(HttpContext httpContext, string prefix)
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var user = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon";
                var actorType = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "anonymous";
                return $"{prefix}:{actorType}:{user}:{ip}";
            }

            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext, "auth"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("otp", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext, "otp"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 6,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("odata", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext, "odata"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static HashSet<string> ResolveAllowedCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sectionOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (sectionOrigins != null)
        {
            foreach (var item in sectionOrigins)
            {
                var normalized = NormalizeCorsOrigin(item);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    origins.Add(normalized);
                }
            }
        }

        var envOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS")
            ?? configuration["Cors:AllowedOrigins"];
        if (!string.IsNullOrWhiteSpace(envOrigins))
        {
            foreach (var item in envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = NormalizeCorsOrigin(item);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    origins.Add(normalized);
                }
            }
        }

        var frontendBaseUrl = NormalizeCorsOrigin(configuration["Frontend:BaseUrl"]);
        if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            origins.Add(frontendBaseUrl);
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            origins.Add("http://localhost:5173");
            origins.Add("https://localhost:5173");
            origins.Add("http://127.0.0.1:5173");
            origins.Add("https://127.0.0.1:5173");
            origins.Add("http://localhost:5062");
            origins.Add("https://localhost:7009");
        }

        return origins;
    }

    private static bool IsAllowedCorsOrigin(string origin, HashSet<string> allowedOrigins, IHostEnvironment environment)
    {
        var normalized = NormalizeCorsOrigin(origin);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (allowedOrigins.Contains(normalized) ||
            string.Equals(normalized, "https://accounts.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((environment.IsDevelopment() || environment.IsEnvironment("Testing")) &&
            Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? NormalizeCorsOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Authority}"
            : null;
    }
}
