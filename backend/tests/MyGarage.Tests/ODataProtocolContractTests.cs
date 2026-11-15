using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataProtocolContractTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task Metadata_ExposesExpectedEntitySets_ForBothVersions(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var response = await client.GetAsync($"{prefix}/$metadata");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {payload}");
        Assert.Contains("EntitySet Name=\"Customers\"", payload);
        Assert.Contains("EntitySet Name=\"Invoices\"", payload);
        Assert.Contains("EntitySet Name=\"AuditLogs\"", payload);
    }

    [Theory]
    [InlineData("$apply=aggregate(Id%20with%20sum%20as%20Total)")]
    [InlineData("$search=test")]
    [InlineData("$expand=Customer")]
    [InlineData("$filter=Id%20add%201%20eq%202")]
    public async Task ServiceOrders_RejectsUnsupportedOperations(string query)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var response = await client.GetAsync($"/odata/ServiceOrders?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
