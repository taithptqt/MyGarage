using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataContinuationScopeTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task ContinuationFromAnotherBranch_IsAlwaysScopedToCurrentActor(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff
            {
                Id = 202, FullName = "Branch 2", Phone = "0901234202", Email = "staff202@example.com",
                PasswordHash = "hash", BranchId = 2, Status = "ACTIVE", SecurityStamp = "test-stamp"
            });
            db.VehicleModels.Add(new VehicleModel { Id = 301, BrandId = 1, ModelName = "Scope" });
            db.Vehicles.Add(new Vehicle { Id = 300, ModelId = 301, LicensePlate = "SCOPE" });
            for (var i = 0; i < 105; i++)
            {
                db.ServiceOrders.Add(new ServiceOrder
                {
                    Id = 1000 + 2 * i, Code = $"B1-{i}", CustomerId = 100, VehicleId = 300,
                    ExecuteBranchId = 1, Status = "CHECKED_IN"
                });
                db.ServiceOrders.Add(new ServiceOrder
                {
                    Id = 1001 + 2 * i, Code = $"B2-{i}", CustomerId = 100, VehicleId = 300,
                    ExecuteBranchId = 2, Status = "CHECKED_IN"
                });
            }
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        using var first = await client.GetAsync($"{prefix}/ServiceOrders?$orderby=Id&$select=Id");
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, firstBody);
        using var firstJson = JsonDocument.Parse(firstBody);
        var next = firstJson.RootElement.GetProperty("@odata.nextLink").GetString();
        Assert.NotNull(next);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(202, "Staff", "MANAGER", 2));
        using var second = await client.GetAsync(next);
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.True(second.StatusCode == HttpStatusCode.OK, secondBody);
        using var secondJson = JsonDocument.Parse(secondBody);
        var returned = secondJson.RootElement.GetProperty("value").EnumerateArray()
            .Select(row => row.GetProperty("Id").GetInt64()).ToArray();
        Assert.NotEmpty(returned);
        Assert.All(returned, id => Assert.Equal(1, id % 2));
    }
}
