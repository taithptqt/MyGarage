using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class CustomerAdministrationPaginationTests
{
    [Fact]
    public async Task CustomerPage_SearchesPrimaryVehicleAndPreservesBranchTotals()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.CustomerTiers.Add(new CustomerTier { Id = 10, TierName = "Gold" });
            db.VehicleBrands.Add(new VehicleBrand { Id = 11, Name = "Toyota", Code = "TOY" });
            db.VehicleModels.Add(new VehicleModel { Id = 12, BrandId = 11, ModelName = "Camry" });
            for (var i = 1; i <= 15; i++)
            {
                db.Customers.Add(new Customer { Id = 1000 + i, FullName = $"Client {i}", Type = "INDIVIDUAL", TierId = 10,
                    IsActive = i % 2 == 0, Note = i <= 12 ? "[PreferredBranchId:1]" : "[PreferredBranchId:2]" });
                db.Vehicles.Add(new Vehicle { Id = 2000 + i, ModelId = 12, LicensePlate = $"TEST-{i:D2}" });
                db.CustomerVehicles.Add(new CustomerVehicle { Id = 3000 + i, CustomerId = 1000 + i,
                    VehicleId = 2000 + i, IsCurrentOwner = true });
            }
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/customers/admin-list?search=camry&page=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(12, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(12, json.RootElement.GetProperty("totalCustomers").GetInt32());
        Assert.Equal(6, json.RootElement.GetProperty("activeCustomers").GetInt32());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("TEST-11", items[0].GetProperty("licensePlate").GetString());
        Assert.Equal(3011, items[0].GetProperty("customerVehicleId").GetInt64());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customers/admin-list?branchId=2")).StatusCode);
    }
}
