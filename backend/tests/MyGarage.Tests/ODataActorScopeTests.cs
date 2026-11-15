using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataActorScopeTests
{
    [Fact]
    public async Task UnrecognizedRole_IsNotTreatedAsCustomer_WhenIdsOverlap()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleModels.Add(new VehicleModel { Id = 311, BrandId = 1, ModelName = "Model" });
            db.Vehicles.Add(new Vehicle { Id = 310, ModelId = 311, LicensePlate = "SCOPE-310" });
            db.CustomerVehicles.Add(new CustomerVehicle { Id = 310, CustomerId = 100, VehicleId = 310, IsCurrentOwner = true });
            db.ServiceOrders.Add(new ServiceOrder { Id = 310, Code = "SCOPE-310", CustomerId = 100, VehicleId = 310, ExecuteBranchId = 1, Status = "CHECKED_IN" });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IODataQueryService>();
        var unknown = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "100"), new Claim(ClaimTypes.Role, "Partner")
        }, "test"));
        Assert.Empty(query.ServiceOrders(unknown));
        Assert.Empty(query.Quotations(unknown));
        Assert.Empty(query.Vehicles(unknown));
        Assert.Empty(query.Appointments(unknown));
    }
}
