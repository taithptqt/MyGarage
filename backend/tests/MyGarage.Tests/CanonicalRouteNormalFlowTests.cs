using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace MyGarage.Tests;

public sealed class CanonicalRouteNormalFlowTests
{
    [Fact]
    public async Task AdminReadRoutes_PreserveLegacyResponses()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var routes = new[]
        {
            ("/api/v1/staff?page=1&pageSize=10", "/api/v1/staffs?page=1&pageSize=10"),
            ("/api/v1/admin/bays", "/api/v1/bay-admin/bays"),
            ("/api/v1/admin/vehicles", "/api/v1/vehicle-admin/list"),
            ("/api/v1/staff/current/payslips", "/api/v1/payslips/my")
        };
        foreach (var (canonical, legacy) in routes)
        {
            using var current = await client.GetAsync(canonical);
            using var previous = await client.GetAsync(legacy);
            var body = await current.Content.ReadAsStringAsync();
            Assert.True(current.StatusCode == HttpStatusCode.OK, $"{canonical}: {(int)current.StatusCode} {body}");
            Assert.Equal(current.StatusCode, previous.StatusCode);
            Assert.Equal(body, await previous.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task CustomerConversationList_PreservesLegacyResponse()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        using var current = await client.GetAsync("/api/v1/chat/conversations");
        using var previous = await client.GetAsync("/api/v1/chat/my-conversations");
        var body = await current.Content.ReadAsStringAsync();
        Assert.True(current.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(current.StatusCode, previous.StatusCode);
        Assert.Equal(body, await previous.Content.ReadAsStringAsync());
    }
}