using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class VehicleModelAdministrationPaginationTests
{
    [Fact]
    public async Task ModelList_FiltersBrandBeforePagingAndKeepsUnfilteredSummary()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.AddRange(
                new VehicleBrand { Id = 1, Code = "JP", Name = "Japan Motors" },
                new VehicleBrand { Id = 2, Code = "DE", Name = "German Motors" });
            for (var i = 1; i <= 25; i++)
                db.VehicleModels.Add(new VehicleModel { Id = i, BrandId = i <= 23 ? 1 : 2,
                    ModelName = $"Model {i}", Segment = "Sedan", FuelType = "Petrol", IsDeleted = i > 20 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/vehicle-models/admin-list?search=japan&page=3&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(23, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(25, json.RootElement.GetProperty("totalModels").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("brandCount").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("activeModels").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(21, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());

        var empty = await client.GetAsync("/api/vehicle-models/admin-list?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyJson.RootElement.GetProperty("items").GetArrayLength());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/vehicle-models/admin-list")).StatusCode);
    }
}
