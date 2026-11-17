using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Application.Bays;
using MyGarage.Data;
using MyGarage.Infrastructure.Persistence.Stores.Common;
using MyGarage.Infrastructure.Queries;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class BayAssignmentGuardTests
{
    [Fact]
    public async Task Assign_RejectsVehicleAlreadyInSameBay()
    {
        await using var db = CreateContext();
        db.Bays.Add(new Bay { Id = 1, BranchId = 1, Code = "A", Name = "A", BayType = "REPAIR", ServiceArea = "A", OperationalStatus = "ACTIVE", CurrentOccupiedVehicleId = 5 });
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "X", Name = "X" });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "X" });
        db.Vehicles.Add(new Vehicle { Id = 5, ModelId = 1, LicensePlate = "51A-12345" });
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBayGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = new EfBayAdminDataService(db);
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            var service = new BayAdminVehicleAssignmentService(data, data, store);
            var result = await service.AssignVehicleAsync(1, new AssignVehicleRequest { VehicleId = 5 }, 1);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("đã được gán", System.Text.Json.JsonSerializer.SerializeToElement(result.Payload).GetProperty("message").GetString());
            Assert.Equal(5, (await db.Bays.FindAsync(1L))!.CurrentOccupiedVehicleId);
        }
        finally
        {
            var config = Path.Combine(root, "Configurations");
            if (Directory.Exists(config)) Directory.Delete(config);
            Directory.Delete(root);
        }
    }

    [Fact]
    public async Task Move_RejectsDifferentBranchesWithoutChangingOccupancy()
    {
        await using var db = CreateContext();
        db.Bays.AddRange(
            new Bay { Id = 1, BranchId = 1, Code = "A", Name = "A", BayType = "REPAIR", ServiceArea = "A", CurrentOccupiedVehicleId = 5 },
            new Bay { Id = 2, BranchId = 2, Code = "B", Name = "B", BayType = "REPAIR", ServiceArea = "B" });
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBayGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = new EfBayAdminDataService(db);
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            var service = new BayAdminVehicleAssignmentService(data, data, store);
            var result = await service.MoveVehicleAsync(new MoveVehicleRequest { VehicleId = 5, FromBayId = 1, ToBayId = 2 }, 1);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal(5, (await db.Bays.FindAsync(1L))!.CurrentOccupiedVehicleId);
            Assert.Null((await db.Bays.FindAsync(2L))!.CurrentOccupiedVehicleId);
        }
        finally { var config = Path.Combine(root, "Configurations"); if (Directory.Exists(config)) Directory.Delete(config); Directory.Delete(root); }
    }

    [Theory]
    [InlineData("UNDER_MAINTENANCE")]
    [InlineData("BROKEN")]
    [InlineData("SUSPENDED")]
    [InlineData("UNRECOGNIZED")]
    public async Task Assign_RejectsUnavailableBay(string status)
    {
        await using var db = CreateContext();
        db.Bays.Add(new Bay { Id = 1, BranchId = 1, Code = "A", Name = "A", BayType = "REPAIR", ServiceArea = "A", OperationalStatus = status });
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "X", Name = "X" });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "X" });
        db.Vehicles.Add(new Vehicle { Id = 5, ModelId = 1, LicensePlate = "51A-12345" });
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBayGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = new EfBayAdminDataService(db);
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            var service = new BayAdminVehicleAssignmentService(data, data, store);
            var result = await service.AssignVehicleAsync(1, new AssignVehicleRequest { VehicleId = 5 }, 1);
            Assert.Equal(400, result.StatusCode);
            Assert.Null((await db.Bays.FindAsync(1L))!.CurrentOccupiedVehicleId);
        }
        finally { var config = Path.Combine(root, "Configurations"); if (Directory.Exists(config)) Directory.Delete(config); Directory.Delete(root); }
    }

    [Fact]
    public async Task Remove_OtherVehiclePreservesExistingOccupancy()
    {
        await using var db = CreateContext();
        db.Bays.Add(new Bay { Id = 1, BranchId = 1, Code = "A", Name = "A", BayType = "REPAIR", ServiceArea = "A", OperationalStatus = "IN_USE", CurrentOccupiedVehicleId = 5 });
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBayGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = new EfBayAdminDataService(db);
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            var service = new BayAdminVehicleAssignmentService(data, data, store);
            var result = await service.RemoveVehicleFromBayAsync(1, new RemoveVehicleRequest { VehicleId = 6 }, 1);
            Assert.Equal(400, result.StatusCode);
            var bay = (await db.Bays.FindAsync(1L))!;
            Assert.Equal(5, bay.CurrentOccupiedVehicleId);
            Assert.Equal("IN_USE", bay.OperationalStatus);
        }
        finally { var config = Path.Combine(root, "Configurations"); if (Directory.Exists(config)) Directory.Delete(config); Directory.Delete(root); }
    }

    [Fact]
    public async Task Move_UpdatesSourceAndTargetOccupancyStatuses()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new GarageContext(options);
        db.Bays.AddRange(
            new Bay { Id = 1, BranchId = 1, Code = "A", Name = "A", BayType = "REPAIR", ServiceArea = "A", OperationalStatus = "IN_USE", CurrentOccupiedVehicleId = 5 },
            new Bay { Id = 2, BranchId = 1, Code = "B", Name = "B", BayType = "REPAIR", ServiceArea = "B", OperationalStatus = "ACTIVE" });
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "X", Name = "X" });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "X" });
        db.Vehicles.Add(new Vehicle { Id = 5, ModelId = 1, LicensePlate = "51A-12345" });
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBayGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var data = new EfBayAdminDataService(db);
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            var service = new BayAdminVehicleAssignmentService(data, data, store);
            var result = await service.MoveVehicleAsync(new MoveVehicleRequest { VehicleId = 5, FromBayId = 1, ToBayId = 2 }, 1);
            Assert.Equal(200, result.StatusCode);
            var source = (await db.Bays.FindAsync(1L))!;
            var target = (await db.Bays.FindAsync(2L))!;
            Assert.Null(source.CurrentOccupiedVehicleId);
            Assert.Equal("ACTIVE", source.OperationalStatus);
            Assert.Equal(5, target.CurrentOccupiedVehicleId);
            Assert.Equal("IN_USE", target.OperationalStatus);
        }
        finally
        {
            var config = Path.Combine(root, "Configurations");
            var settings = Path.Combine(config, "bay-admin-settings.json");
            if (File.Exists(settings)) File.Delete(settings);
            if (Directory.Exists(config)) Directory.Delete(config);
            Directory.Delete(root);
        }
    }

    private static GarageContext CreateContext() => new(new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "MyGarage.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
