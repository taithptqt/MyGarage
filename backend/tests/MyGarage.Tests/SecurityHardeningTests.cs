using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using MyGarage.Security;
using Xunit;

namespace MyGarage.Tests;

public class SecurityHardeningTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk_WithSecurityHeaders()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff);
        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frameOptions));
        Assert.Contains("DENY", frameOptions);
    }

    [Fact]
    public async Task ProtectedEndpoints_RequireAuthentication()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var odataResponse = await client.GetAsync("/odata/ServiceOrders");
        Assert.Equal(HttpStatusCode.Unauthorized, odataResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerRole_IsForbiddenFromAdminAndStaffEndpoints()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var customerToken = TestWebApplicationFactory.CreateJwtToken(100, "Customer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var auditResponse = await client.GetAsync("/api/v1/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, auditResponse.StatusCode);

        var rolesResponse = await client.GetAsync("/api/v1/roles");
        Assert.Equal(HttpStatusCode.Forbidden, rolesResponse.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsEndpoint_ReturnsNotFound_OutsideDevelopment()
    {
        await using var factory = new TestWebApplicationFactory("Production");
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var adminToken = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/v1/diagnostics/email-config");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CookieAuth_BlocksStateChangingRequest_WhenOriginMissingOrEvil_AndAllowsTrustedOrigin()
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

        using var preLogoutProfileReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        preLogoutProfileReq.Headers.Add("Cookie", $"{AuthCookie.Name}={staffToken}");
        var preLogoutProfileRes = await client.SendAsync(preLogoutProfileReq);
        Assert.Equal(HttpStatusCode.OK, preLogoutProfileRes.StatusCode);

        using var noOriginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session-revocations");
        noOriginReq.Headers.Add("Cookie", $"{AuthCookie.Name}={staffToken}");
        var noOriginRes = await client.SendAsync(noOriginReq);
        Assert.Equal(HttpStatusCode.Forbidden, noOriginRes.StatusCode);

        using var evilOriginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session-revocations");
        evilOriginReq.Headers.Add("Cookie", $"{AuthCookie.Name}={staffToken}");
        evilOriginReq.Headers.Add("Origin", "https://evil.attacker.example");
        var evilOriginRes = await client.SendAsync(evilOriginReq);
        Assert.Equal(HttpStatusCode.Forbidden, evilOriginRes.StatusCode);

        using var trustedOriginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/session-revocations");
        trustedOriginReq.Headers.Add("Cookie", $"{AuthCookie.Name}={staffToken}");
        trustedOriginReq.Headers.Add("Origin", "https://localhost:5173");
        var trustedOriginRes = await client.SendAsync(trustedOriginReq);
        Assert.Equal(HttpStatusCode.OK, trustedOriginRes.StatusCode);

        // Token revoked by logout must immediately be rejected on subsequent requests
        using var reusedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        reusedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var reusedRes = await client.SendAsync(reusedReq);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedRes.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_GeneratesCleanly_InDevelopmentWithOData()
    {
        await using var factory = new TestWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("MyGarage", body);
    }
}
