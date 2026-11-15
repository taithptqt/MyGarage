using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class BranchAdministrationPaginationTests
{
    [Fact]
    public async Task BranchPage_FiltersBeforePaging_AndKeepsGlobalSummary()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 30; i++)
                db.Branches.Add(new Branch { Id = 100 + i, Name = $"Store {i}", Email = "find@example.com",
                    Status = i % 2 == 0 ? "ACTIVE" : "INACTIVE" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var result = await client.GetAsync("/api/v1/branches/admin-list?search=find&page=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(30, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(32, json.RootElement.GetProperty("totalBranches").GetInt32());
        Assert.Equal(17, json.RootElement.GetProperty("activeBranches").GetInt32());
        Assert.Equal(111, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/branches/admin-list")).StatusCode);
    }
}
