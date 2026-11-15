using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyGarage.Security;
using MyGarage.Interfaces;
using MyGarage.Services;
using MyGarage.Data;
using AutoMapper;
using Xunit;

namespace MyGarage.Tests;

public class ConfigurationAndSecurityTests
{
    private sealed class RecursiveNode
    {
        public RecursiveNode? Next { get; set; }
    }

    [Fact]
    public void AutoMapper_DepthLimitStopsDeepRecursiveMapping()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<RecursiveNode, RecursiveNode>();
            MappingDepthLimit.Apply(cfg);
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var root = new RecursiveNode();
        var current = root;
        for (var i = 0; i < 1000; i++) current = current.Next = new RecursiveNode();
        var mapped = config.CreateMapper().Map<RecursiveNode>(root);
        var depth = 0;
        for (var node = mapped; node != null; node = node.Next) depth++;
        Assert.InRange(depth, 1, 64);
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".env.example")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root with .env.example");
    }

    [Fact]
    public void AppSettings_DoesNotContainLiveSecretsOrCredentials()
    {
        var repoRoot = ResolveRepoRoot();
        var appsettingsPath = Path.Combine(repoRoot, "backend", "src", "MyGarage.Api", "appsettings.json");
        var devSettingsPath = Path.Combine(repoRoot, "backend", "src", "MyGarage.Api", "appsettings.Development.json");

        Assert.True(File.Exists(appsettingsPath), $"appsettings.json not found at {appsettingsPath}");
        Assert.True(File.Exists(devSettingsPath), $"appsettings.Development.json not found at {devSettingsPath}");

        var appsettingsText = File.ReadAllText(appsettingsPath);
        var devSettingsText = File.ReadAllText(devSettingsPath);

        using var prodDoc = JsonDocument.Parse(appsettingsText);
        using var devDoc = JsonDocument.Parse(devSettingsText);

        // 1. Connection string must be a placeholder, not a real database password
        var connStr = prodDoc.RootElement.GetProperty("ConnectionStrings").GetProperty("GarageDbConnection").GetString() ?? "";
        Assert.Contains("YOUR_SQL_SERVER", connStr);
        Assert.Contains("YOUR_SQL_PASSWORD", connStr);

        // 2. JWT Key must be a placeholder
        var jwtKey = prodDoc.RootElement.GetProperty("Jwt").GetProperty("Key").GetString() ?? "";
        Assert.Contains("YOUR_JWT_KEY", jwtKey);

        // 3. Email password must be a placeholder
        var emailPass = prodDoc.RootElement.GetProperty("EmailSettings").GetProperty("Password").GetString() ?? "";
        Assert.Equal("your-gmail-app-password", emailPass);

        // 4. PayOS credentials in Development must be placeholders
        var payosApiKey = devDoc.RootElement.GetProperty("PayOS").GetProperty("ApiKey").GetString() ?? "";
        Assert.Equal("your-payos-api-key", payosApiKey);

        var payosChecksumKey = devDoc.RootElement.GetProperty("PayOS").GetProperty("ChecksumKey").GetString() ?? "";
        Assert.Equal("your-payos-checksum-key", payosChecksumKey);

        // 5. Azure Storage key must be a placeholder
        var storageConn = prodDoc.RootElement.GetProperty("AzureStorage").GetProperty("ConnectionString").GetString() ?? "";
        Assert.Contains("YOUR_STORAGE_ACCOUNT_KEY", storageConn);

        // 6. Gemini OpenAI key must be a placeholder
        var geminiKey = prodDoc.RootElement
            .GetProperty("LlmGateway")
            .GetProperty("Providers")
            .GetProperty("GEMINI_OPENAI")
            .GetProperty("DefaultApiKey").GetString() ?? "";
        Assert.Equal("YOUR_GEMINI_API_KEY", geminiKey);
    }

    [Fact]
    public void DotEnvExample_ContainsExpectedTemplates_WithoutLiveSecrets()
    {
        var repoRoot = ResolveRepoRoot();
        var envExamplePath = Path.Combine(repoRoot, ".env.example");

        Assert.True(File.Exists(envExamplePath), $".env.example not found at {envExamplePath}");

        var envText = File.ReadAllText(envExamplePath);

        Assert.Contains("ConnectionStrings__GarageDbConnection", envText);
        Assert.Contains("Jwt__Key", envText);
        Assert.Contains("EmailSettings__Password", envText);
        Assert.Contains("PayOS__ApiKey", envText);
        Assert.Contains("AzureStorage__ConnectionString", envText);
        Assert.Contains("LlmGateway__Providers__GEMINI_OPENAI__DefaultApiKey", envText);

        Assert.Contains("YOUR_SQL_PASSWORD", envText);
        Assert.Contains("your-payos-api-key", envText);
    }

    [Fact]
    public async Task SessionCookieAuth_AuthenticatesProtectedGetEndpoint()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var staffToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        request.Headers.Add("Cookie", $"{AuthCookie.Name}={staffToken}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SessionCookieAuth_InvalidToken_ReturnsUnauthorized()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        request.Headers.Add("Cookie", $"{AuthCookie.Name}=completely-invalid-jwt-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CsrfMiddleware_SafeGetMethod_DoesNotRequireOriginHeader()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var staffToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);

        // Safe GET with cookie, NO Origin or Referer header
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        request.Headers.Add("Cookie", $"{AuthCookie.Name}={staffToken}");

        var response = await client.SendAsync(request);

        // Safe GET must not be blocked by CSRF origin validation
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CsrfMiddleware_BearerAuthWithoutCookie_ExemptFromOriginRequirement()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var staffToken = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);

        // POST request authenticated purely via Bearer token (no cookie) and NO Origin header
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session-revocations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var response = await client.SendAsync(request);

        // Pure Bearer token requests are immune to ambient CSRF attacks and should not be blocked with 403 Forbidden
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForwardedHeaders_ProcessesRequestsCorrectly()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.195");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void ForwardedHeaders_DoesNotTrustArbitraryRemoteProxies()
    {
        using var factory = new TestWebApplicationFactory();
        var options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.DoesNotContain(options.KnownProxies, address => address.Equals(IPAddress.Parse("203.0.113.195")));
        Assert.DoesNotContain(options.KnownIPNetworks, network => network.Contains(IPAddress.Parse("203.0.113.195")));
    }

    [Fact]
    public async Task Logout_RevocationSurvivesNewBlacklistInstance()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        var token = TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/session-revocations", null)).StatusCode);
        var independentInstance = new TokenBlacklistService(factory.Services.GetRequiredService<IServiceScopeFactory>());
        Assert.True(await independentInstance.IsTokenRevokedAsync(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/vehicles")).StatusCode);
    }

    [Fact]
    public async Task DisabledStaff_IsRejectedImmediatelyEvenAfterPreviousSuccessfulRequest()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/vehicles")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
            var staff = await db.Staffs.FindAsync(200L);
            staff!.Status = "TERMINATED";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/vehicles")).StatusCode);
    }
}
