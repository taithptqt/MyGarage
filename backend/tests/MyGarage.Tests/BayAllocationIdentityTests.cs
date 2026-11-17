using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Application.Bays.DTOs;
using MyGarage.Data;
using MyGarage.Exceptions;
using MyGarage.Infrastructure.Bays;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class BayAllocationIdentityTests
{
    [Fact]
    public async Task Transfer_RejectsLockedTargetWithoutReleasingSource()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Bays.AddRange(
                new Bay { Id = 1, BranchId = 1, Code = "B1", Name = "Khoang 1", BayType = "REPAIR", ServiceArea = "General", OperationalStatus = "IN_USE", IsActive = true, CurrentOccupiedVehicleId = 1 },
                new Bay { Id = 2, BranchId = 1, Code = "B2", Name = "Khoang 2", BayType = "REPAIR", ServiceArea = "General", OperationalStatus = "ACTIVE", IsActive = false });
            db.BayAllocations.Add(new BayAllocation { Id = 1, BayId = 1, VehicleId = 1, ServiceOrderId = 1, AllocatedBy = 201, AllocatedAt = DateTime.Now });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBayApplicationService>();
        var actor = new UserActorContext(201, "Staff", "ADMIN", null, true);

        await Assert.ThrowsAsync<BadRequestException>(() => service.TransferBayAsync(1, 2, 1, null, actor));
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Null((await db.BayAllocations.FindAsync(1L))!.ReleasedAt);
        Assert.Equal(1, await db.BayAllocations.CountAsync());
    }

    [Fact]
    public async Task ActiveAllocationLookup_RejectsAmbiguousBayAndVehicle()
    {
        var options = new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GarageContext(options);
        db.BayAllocations.AddRange(
            new BayAllocation { Id = 1, BayId = 1, VehicleId = 1, ServiceOrderId = 1, AllocatedBy = 201, AllocatedAt = DateTime.Now },
            new BayAllocation { Id = 2, BayId = 1, VehicleId = 1, ServiceOrderId = 1, AllocatedBy = 201, AllocatedAt = DateTime.Now });
        await db.SaveChangesAsync();
        var store = new EfBayStore(db);

        await Assert.ThrowsAsync<BadRequestException>(() => store.GetActiveAllocationForBayAsync(1));
        await Assert.ThrowsAsync<BadRequestException>(() => store.GetActiveAllocationForBayAndVehicleAsync(1, 1));
        Assert.Equal(2, await db.BayAllocations.CountAsync(item => item.ReleasedAt == null));
    }

    [Fact]
    public async Task Allocation_RejectsLockedBay()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Bays.Add(new Bay { Id = 1, BranchId = 1, Code = "B1", Name = "Khoang 1", BayType = "REPAIR", ServiceArea = "General", OperationalStatus = "ACTIVE", IsActive = false });
            db.ServiceOrders.Add(new ServiceOrder { Id = 1, Code = "SO-1", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "IN_PROGRESS" });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBayApplicationService>();
        var actor = new UserActorContext(201, "Staff", "ADMIN", null, true);
        await Assert.ThrowsAsync<BadRequestException>(() => service.AllocateBayAsync(new BayAllocationInput(1, 1, 1, null, null), actor));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GarageContext>().BayAllocations.ToListAsync());
    }

    [Theory]
    [InlineData("IN_PROGRESS", 2L, false)]
    [InlineData("CLOSED", 1L, false)]
    [InlineData("CANCELED", 1L, false)]
    [InlineData("IN_PROGRESS", 1L, true)]
    public async Task Allocation_RejectsWrongVehicleOrEndedOrder(string status, long vehicleId, bool handedOver)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Bays.Add(new Bay { Id = 1, BranchId = 1, Code = "B1", Name = "Khoang 1", BayType = "REPAIR", ServiceArea = "General", OperationalStatus = "ACTIVE" });
            db.Vehicles.AddRange(new Vehicle { Id = 1, LicensePlate = "51A-11111", ModelId = 1 }, new Vehicle { Id = 2, LicensePlate = "51A-22222", ModelId = 1 });
            db.ServiceOrders.Add(new ServiceOrder { Id = 1, Code = "SO-1", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = status, ActualHandover = handedOver ? DateTime.Now : null });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBayApplicationService>();
        var actor = new UserActorContext(201, "Staff", "ADMIN", null, true);

        await Assert.ThrowsAsync<BadRequestException>(() => service.AllocateBayAsync(new BayAllocationInput(1, 1, vehicleId, null, null), actor));
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Empty(await db.BayAllocations.ToListAsync());
        Assert.Null((await db.Bays.FindAsync(1L))!.CurrentOccupiedVehicleId);
    }
}
