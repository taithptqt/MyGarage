using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class VehicleBrandAdministrationPaginationTests
{
    [Fact]
    public async Task BrandList_FiltersBeforePagingAndKeepsUnfilteredSummary()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 25; i++)
                db.VehicleBrands.Add(new VehicleBrand { Id = i, Code = $"BR{i:D2}", Name = $"Brand {i}",
                    CountryOfOrigin = i <= 23 ? "Japan" : "Germany", IsActive = i <= 15,
                    IsDeleted = i == 25 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/vehicle-brands/admin-list?search=japan&page=3&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(23, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(25, json.RootElement.GetProperty("totalBrands").GetInt32());
        Assert.Equal(15, json.RootElement.GetProperty("activeBrands").GetInt32());
        Assert.Equal(10, json.RootElement.GetProperty("inactiveBrands").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(21, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());

        var empty = await client.GetAsync("/api/vehicle-brands/admin-list?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyJson.RootElement.GetProperty("items").GetArrayLength());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/vehicle-brands/admin-list")).StatusCode);
    }
}
