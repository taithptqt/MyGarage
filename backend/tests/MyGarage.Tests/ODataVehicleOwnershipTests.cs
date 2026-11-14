using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataVehicleOwnershipTests
{
    [Fact]
    public async Task Vehicles_ExcludesDeletedAndFormerOwnershipLinks()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleModels.Add(new VehicleModel { Id = 301, BrandId = 1, ModelName = "Test" });
            db.Vehicles.AddRange(
                new Vehicle { Id = 301, ModelId = 301, LicensePlate = "CURRENT" },
                new Vehicle { Id = 302, ModelId = 301, LicensePlate = "DELETED" },
                new Vehicle { Id = 303, ModelId = 301, LicensePlate = "FORMER" },
                new Vehicle { Id = 304, ModelId = 301, LicensePlate = "LEGACY" });
            db.CustomerVehicles.AddRange(
                new CustomerVehicle { Id = 1, CustomerId = 100, VehicleId = 301, IsCurrentOwner = true },
                new CustomerVehicle { Id = 2, CustomerId = 100, VehicleId = 302, IsDeleted = true, IsCurrentOwner = true },
                new CustomerVehicle { Id = 3, CustomerId = 100, VehicleId = 303, IsCurrentOwner = false },
                new CustomerVehicle { Id = 4, CustomerId = 100, VehicleId = 304, IsCurrentOwner = null });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        using var response = await client.GetAsync("/odata/Vehicles?$orderby=Id&$select=Id");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement : json.RootElement.GetProperty("value");
        Assert.Equal(new long[] { 301, 304 }, rows.EnumerateArray().Select(row => row.GetProperty("Id").GetInt64()));
    }
}
