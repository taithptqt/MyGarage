using System.Net;
using System.Net.Http.Headers;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class VehicleDeletedOwnershipTests
{
    [Fact]
    public async Task DeletedCustomerVehicle_DoesNotGrantVehicleOrHistoryAccess()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Vehicles.Add(new Vehicle { Id = 300, ModelId = 301, LicensePlate = "30A-11111" });
            db.CustomerVehicles.Add(new CustomerVehicle { CustomerId = 100, VehicleId = 300, IsDeleted = true });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/vehicles/300")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/vehicles/300/service-history")).StatusCode);
    }
}
