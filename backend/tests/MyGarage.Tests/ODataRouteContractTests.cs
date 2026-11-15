using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataRouteContractTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task Paging_ProvidesAWorkingNextLinkOnBothPrefixes(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 0; i < 105; i++)
                db.Customers.Add(new Customer { Id = 1000 + i, FullName = $"Page {i}", Phone = $"09{i:D8}", Type = "INDIVIDUAL" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        using var response = await client.GetAsync($"{prefix}/Customers?$orderby=Id&$select=Id&$count=true");
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload);
        Assert.Equal(106, json.RootElement.GetProperty("@odata.count").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("value").GetArrayLength());
        var next = json.RootElement.GetProperty("@odata.nextLink").GetString();
        Assert.NotNull(next);
        Assert.Contains(prefix + "/Customers", next);
        using var second = await client.GetAsync(next);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(6, secondJson.RootElement.GetProperty("value").GetArrayLength());
    }

    [Theory]
    [InlineData("Appointments")]
    [InlineData("AuditLogs")]
    [InlineData("Customers")]
    [InlineData("Inventory")]
    [InlineData("Invoices")]
    [InlineData("Quotations")]
    [InlineData("ServiceOrders")]
    [InlineData("Staffs")]
    [InlineData("Vehicles")]
    public async Task Routes_RequireAuthentication_AndSupportBothPrefixes(string entity)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/odata/{entity}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/odata/{entity}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        foreach (var prefix in new[] { "/odata", "/api/v1/odata" })
        {
            using var response = await client.GetAsync($"{prefix}/{entity}?$top=1&$select=Id");
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{prefix}/{entity}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{prefix}/{entity}?$top=101")).StatusCode);
        }
    }
}
