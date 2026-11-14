using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Application.Authorization;
using MyGarage.Application.CustomerPortal.DTOs;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class CustomerPortalVehicleIdentityTests
{
    [Fact]
    public async Task RegistrationAndUpdate_ReturnSavedValuesAndTrackOdometerChanges()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new GarageContext(options, new HttpContextAccessor());
        var brand = new VehicleBrand { Id = 1, Name = "Brand", Code = "B" };
        var model = new VehicleModel { Id = 1, Brand = brand, ModelName = "Model" };
        var customer = new Customer { Id = 100, FullName = "Customer", Type = "INDIVIDUAL" };
        var oldOdoTime = new DateTime(2020, 1, 1);
        var vehicle = new Vehicle { Id = 1, Model = model, LicensePlate = "51K-12345", Vin = "1HGCR2F83HA123456", ManufactureYear = 2020, Color = "Blue", Odo = 100, OdoLastUpdatedAt = oldOdoTime };
        db.AddRange(customer, vehicle, new CustomerVehicle { Customer = customer, Vehicle = vehicle, IsCurrentOwner = true });
        await db.SaveChangesAsync();
        var service = new CustomerPortalApplicationService(db, new CustomerPortalAuthorizationService(db));
        var actor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);
        CustomerPortalVehicleUpsertCommand Command(int? odo) => new("51K-12345", 1, null, null, null, null, null, odo);

        var reused = await service.RegisterVehicleAsync(100, Command(null), actor);
        Assert.Equal(2020, reused.ManufactureYear);
        Assert.Equal("Blue", reused.Color);
        Assert.Equal("1HGCR2F83HA123456", reused.Vin);
        Assert.Equal(100, reused.CurrentOdo);
        Assert.Equal(oldOdoTime, vehicle.OdoLastUpdatedAt);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterVehicleAsync(100, Command(-1), actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateVehicleAsync(100, vehicle.Id, Command(-1), actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterVehicleAsync(100, new("51K-55555", 1, null, null, null, null, null, -1), actor));
        Assert.Equal(100, vehicle.Odo);
        Assert.Single(db.Vehicles);

        var registered = await service.RegisterVehicleAsync(100, Command(200), actor);
        Assert.Equal(200, registered.CurrentOdo);
        Assert.NotNull(vehicle.OdoLastUpdatedAt);
        Assert.NotEqual(oldOdoTime, vehicle.OdoLastUpdatedAt);
        vehicle.OdoLastUpdatedAt = oldOdoTime;
        await db.SaveChangesAsync();
        var updated = await service.UpdateVehicleAsync(100, vehicle.Id, Command(300), actor);
        Assert.Equal(300, updated!.CurrentOdo);
        Assert.NotEqual(oldOdoTime, vehicle.OdoLastUpdatedAt);
        var timestamp = vehicle.OdoLastUpdatedAt;
        await service.UpdateVehicleAsync(100, vehicle.Id, Command(null), actor);
        Assert.Equal(timestamp, vehicle.OdoLastUpdatedAt);

        var created = await service.RegisterVehicleAsync(100, new("51K-55555", 1, null, null, null, null, null, 500), actor);
        Assert.NotNull((await db.Vehicles.SingleAsync(v => v.Id == created.Id)).OdoLastUpdatedAt);
    }

    [Fact]
    public async Task VehicleIdentityChecks_PreserveCaseInsensitiveLegacyBehavior()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new GarageContext(options, new HttpContextAccessor());
        var brand = new VehicleBrand { Id = 1, Name = "Brand", Code = "B" };
        var model = new VehicleModel { Id = 1, Brand = brand, ModelName = "Model" };
        var customer = new Customer { Id = 100, FullName = "Customer", Type = "INDIVIDUAL" };
        var first = new Vehicle { Id = 1, Model = model, LicensePlate = "51k-12345", Vin = "1hgcr2f83ha123456" };
        var other = new Vehicle { Id = 2, Model = model, LicensePlate = "51k-67890", Vin = "2hgcr2f83ha123456" };
        db.AddRange(customer, first, other,
            new CustomerVehicle { Customer = customer, Vehicle = first, IsCurrentOwner = true },
            new CustomerVehicle { Customer = customer, Vehicle = other, IsCurrentOwner = true });
        await db.SaveChangesAsync();
        var service = new CustomerPortalApplicationService(db, new CustomerPortalAuthorizationService(db));
        var actor = new UserActorContext(100, "Customer", "CUSTOMER", null, IsAdmin: false);
        CustomerPortalVehicleUpsertCommand Command(string plate, string? vin) =>
            new(plate, 1, null, null, null, null, vin, null);

        var reused = await service.RegisterVehicleAsync(100, Command("51K-12345", "1HGCR2F83HA123456"), actor);
        Assert.Equal(1, reused.Id);
        Assert.Equal(2, await db.Vehicles.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterVehicleAsync(
            100, Command("51K-55555", "2HGCR2F83HA123456"), actor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterVehicleAsync(
            100, Command("51K-12345", "2HGCR2F83HA123456"), actor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateVehicleAsync(
            100, 1, Command("51K-67890", null), actor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateVehicleAsync(
            100, 1, Command("51K-12345", "2HGCR2F83HA123456"), actor));
        var updated = await service.UpdateVehicleAsync(100, 1, Command("51K-12345", "1HGCR2F83HA123456"), actor);
        Assert.NotNull(updated);
        Assert.Equal("1HGCR2F83HA123456", updated.Vin);
    }
}
