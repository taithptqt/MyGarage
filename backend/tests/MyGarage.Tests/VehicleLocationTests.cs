using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Options;
using MyGarage.Services;
using MyGarage.Controllers;
using MyGarage.Infrastructure.Persistence.Stores.Vehicles;
using System.Text.Json;
using Xunit;

namespace MyGarage.Tests;

public sealed class VehicleLocationTests
{
    private static readonly UserActorContext Tech = new(10, "Staff", "TECH", 1, false);
    [Fact]
    public async Task RepairReports_UseLatestPerPart_PersistHistory_AndStayWithinOwnerScope()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await service.ReportRepairAsync(1, new("tyre-right-front", "Kiểm tra lốp", false), Tech);
        await service.ReportRepairAsync(1, new("windshield", "Thay kính nứt", false), Tech);
        await service.ReportRepairAsync(1, new("tyre-right-front", "  Đang thay lốp mới  ", false), Tech);
        var vehicle = (await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1);
        Assert.Equal(2, vehicle.Repairs.Count);
        var tyre = Assert.Single(vehicle.Repairs, repair => repair.PartId == "tyre-right-front");
        Assert.Equal("Đang thay lốp mới", tyre.Note); Assert.Equal("Tech One", tyre.Technician); Assert.NotNull(tyre.ReportedAt);
        Assert.Empty((await service.GetCustomerGarageAsync(200)).Vehicles.Single().Repairs);
        Assert.Equal(2, (await service.GetTechnicianWorkspaceAsync(Tech)).Orders.Single(order => order.Id == 1).Repairs.Count);
        await service.ReportRepairAsync(1, new("tyre-right-front", "Đã thay lốp", true, tyre.ReportId), Tech);
        Assert.Equal("windshield", Assert.Single((await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1).Repairs).PartId);
        Assert.Equal(4, await db.VehicleConditionLogs.CountAsync(log => log.LogType == "REPAIR_3D"));
        (await db.ServiceOrders.FindAsync(1L))!.Status = "CLOSED"; await db.SaveChangesAsync();
        Assert.Empty((await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1).Repairs);
    }

    [Fact]
    public async Task Completion_RejectsStaleOrMissingReportVersion()
    {
        await using var db = await Seed();
        var service = new VehicleLocationService(db);
        await service.ReportRepairAsync(1, new("windshield", "Lần đầu", false), Tech);
        var first = Assert.Single((await service.GetTechnicianWorkspaceAsync(Tech)).Orders.Single(order => order.Id == 1).Repairs);
        await service.ReportRepairAsync(1, new("windshield", "Cập nhật mới", false), Tech);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportRepairAsync(1, new("windshield", "Xong", true, first.ReportId), Tech));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportRepairAsync(1, new("windshield", "Xong", true), Tech));
        Assert.Equal("Cập nhật mới", Assert.Single((await service.GetTechnicianWorkspaceAsync(Tech)).Orders.Single(order => order.Id == 1).Repairs).Note);
    }

    [Fact]
    public async Task RepairReport_DoesNotAppearAmongVehicleConditionInspections()
    {
        await using var db = await Seed();
        db.VehicleConditionLogs.Add(new VehicleConditionLog { ServiceOrderId = 1, LogType = "CHECK_IN", CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();
        await new VehicleLocationService(db).ReportRepairAsync(1, new("windshield", "Đang thay kính", false), Tech);

        var inspections = await new EfVehicleConditionLogStore(db).GetByOrderAsync(1);
        Assert.Equal("CHECK_IN", Assert.Single(inspections).LogType);
    }

    [Fact]
    public async Task CanceledDetail_DoesNotGrantRepairReportAccess()
    {
        await using var db = await Seed();
        (await db.ServiceOrderDetails.FindAsync(1L))!.Status = "CANCELED";
        await db.SaveChangesAsync();
        var service = new VehicleLocationService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReportRepairAsync(1, new("windshield", "Không còn được giao", false), Tech));
        Assert.DoesNotContain((await service.GetTechnicianWorkspaceAsync(Tech)).Orders, order => order.Id == 1);
    }

    [Fact]
    public async Task RepairReports_RejectUnassignedForeignBranchInvalidPartAndClosedOrder()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReportRepairAsync(2, new("windshield", "Không được giao", false), Tech));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReportRepairAsync(1, new("windshield", "Sai chi nhánh", false), Tech with { BranchId = 2 }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportRepairAsync(1, new("unknown", "Sai bộ phận", false), Tech));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportRepairAsync(1, new("windshield", " ", false), Tech));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportRepairAsync(1, new("windshield", new string('x', 501), false), Tech));
        (await db.ServiceOrders.FindAsync(1L))!.Status = "CLOSED"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReportRepairAsync(1, new("windshield", "Đã đóng", false), Tech));
        Assert.Empty(await db.VehicleConditionLogs.ToListAsync());
    }

    private static async Task<GarageContext> Seed()
    {
        var db = new GarageContext(new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString(), options => options.EnableNullChecks(false)).Options);
        db.Branches.AddRange(new Branch { Id = 1, Name = "Garage One" }, new Branch { Id = 2, Name = "Garage Two" });
        db.Customers.AddRange(new Customer { Id = 100, FullName = "Owner One", Type = "Individual" }, new Customer { Id = 200, FullName = "Owner Two", Type = "Individual" });
        db.Staffs.AddRange(new Staff { Id = 10, FullName = "Tech One", BranchId = 1, PasswordHash = "unused" }, new Staff { Id = 11, FullName = "Tech Two", BranchId = 1, PasswordHash = "unused" });
        db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "VF", Name = "VinFast" });
        db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Lux" });
        for (var i = 1; i <= 3; i++)
        {
            var owner = i == 2 ? 200 : 100;
            var branch = i == 2 ? 2 : 1;
            db.Vehicles.Add(new Vehicle { Id = i, LicensePlate = $"PLATE-{i}", ModelId = 1 });
            db.CustomerVehicles.Add(new CustomerVehicle { Id = i, CustomerId = owner, VehicleId = i, IsCurrentOwner = true });
            db.ServiceOrders.Add(new ServiceOrder { Id = i, VehicleId = i, CustomerId = owner, Code = $"SO-{i}", ExecuteBranchId = branch, OriginBranchId = branch, Status = "IN_PROGRESS" });
            db.ServiceOrderDetails.Add(new ServiceOrderDetail { Id = i, ServiceOrderId = i, ItemType = "SERVICE", ItemName = "Repair" });
        }
        db.ServiceStaffAssignments.AddRange(new ServiceStaffAssignment { Id = 1, ServiceOrderDetailId = 1, TechnicianId = 10, RoleInTask = "MAIN", Status = "IN_PROGRESS" }, new ServiceStaffAssignment { Id = 3, ServiceOrderDetailId = 3, TechnicianId = 10, RoleInTask = "MAIN", Status = "IN_PROGRESS" });
        db.Bays.AddRange(
            new Bay { Id = 1, BranchId = 1, Code = "A01", Name = "Khoang 1", ServiceArea = "Khu sửa chữa", BayType = "REPAIR", OperationalStatus = "ACTIVE", IsActive = true },
            new Bay { Id = 2, BranchId = 1, Code = "A02", Name = "Bãi chờ", ServiceArea = "Bãi A", BayType = "PARKING", OperationalStatus = "ACTIVE", IsActive = true },
            new Bay { Id = 3, BranchId = 2, Code = "A01", Name = "Khoang chi nhánh khác", ServiceArea = "Kho B", BayType = "STORAGE", OperationalStatus = "ACTIVE", IsActive = true });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Report_UpdatesOnlyOwnersGarage_WithRealBranchBayAndReporter()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await service.ReportAsync(1, new(1, null, "Đã đưa xe vào A01"), Tech);
        var garage = await service.GetCustomerGarageAsync(100);
        var vehicle = Assert.Single(garage.Vehicles, vehicle => vehicle.VehicleId == 1);
        Assert.Equal(1, vehicle.BranchId); Assert.Equal(1, vehicle.BayId); Assert.Equal("A01", vehicle.BayCode);
        Assert.Equal("Tech One", vehicle.ReportedBy); Assert.Equal("Đã đưa xe vào A01", vehicle.Note);
        Assert.Equal("ACTIVE", (await db.Bays.FindAsync(1L))!.OperationalStatus);
        Assert.Equal(2, (await db.Bays.FindAsync(1L))!.ReservationStatus);
        Assert.Single(garage.Branches); Assert.Equal(1, garage.Branches[0].Id);
        Assert.All(garage.Branches[0].Bays, bay => Assert.Equal(1, bay.BranchId));
        Assert.DoesNotContain(garage.Vehicles, vehicle => vehicle.VehicleId == 2);
        var other = await service.GetCustomerGarageAsync(200);
        Assert.DoesNotContain(other.Vehicles, vehicle => vehicle.VehicleId == 1);
        Assert.All(other.Branches, branch => Assert.Equal(2, branch.Id));
    }

    [Fact]
    public async Task Move_ReleasesPreviousBay_PreservesHistory_AndFeedsCustomerDashboard()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await service.ReportAsync(1, new(1, null, "Tiếp nhận"), Tech);
        await service.ReportAsync(1, new(2, 1, "Đưa xe sang bãi chờ"), Tech);
        Assert.Null((await db.Bays.FindAsync(1L))!.CurrentOccupiedVehicleId);
        Assert.Equal(1, (await db.Bays.FindAsync(2L))!.CurrentOccupiedVehicleId);
        Assert.Single(await db.BayAllocations.Where(row => row.VehicleId == 1 && row.ReleasedAt == null).ToListAsync());
        Assert.Single(await db.BayAllocations.Where(row => row.VehicleId == 1 && row.ReleasedAt != null).ToListAsync());
        Assert.Equal(3, await db.BayStatusLogs.CountAsync());
        var vehicle = Assert.Single((await service.GetCustomerGarageAsync(100)).Vehicles, item => item.VehicleId == 1);
        Assert.Equal("A02", vehicle.BayCode); Assert.Equal("Bãi A", vehicle.ServiceArea); Assert.Equal("Tech One", vehicle.ReportedBy);
    }

    [Fact]
    public async Task StaleReport_CannotOverwriteNewerLocation()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await service.ReportAsync(1, new(1, null, "First"), Tech);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportAsync(1, new(2, null, "Stale"), Tech));
        Assert.Equal(1, (await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1).BayId);
    }

    [Fact]
    public async Task Report_DeniesForeignBranchUnassignedOrderAndWrongRole()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync(1, new(3, null, "Other branch"), Tech));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReportAsync(2, new(1, null, "Not assigned"), Tech));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetTechnicianWorkspaceAsync(Tech with { Role = "Customer", RoleCode = "CUSTOMER" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetTechnicianWorkspaceAsync(Tech with { BranchId = 2 }));
        Assert.Empty(await db.BayAllocations.ToListAsync());
    }

    [Fact]
    public async Task OneVehicleBay_CannotBeOccupiedByAnotherCar()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await service.ReportAsync(1, new(1, null, "First car"), Tech);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportAsync(3, new(1, null, "Second car"), Tech));
        Assert.Equal(1, await db.BayAllocations.CountAsync());
    }

    [Fact]
    public async Task SharedParking_ReadsExistingSessions_AndHonorsCapacity()
    {
        await using var db = await Seed();
        var source = new SnapshotSource(new([new(2, 2, DateTime.Now.AddDays(-1), null, "Existing parked car")], new Dictionary<long, int> { [2] = 2 }));
        var service = new VehicleLocationService(db, source);
        Assert.Null((await service.GetCustomerGarageAsync(200)).Vehicles.Single().BayId);
        await service.ReportAsync(1, new(2, null, "Second space"), Tech);
        Assert.Equal(2, (await service.GetTechnicianWorkspaceAsync(Tech)).Bays.Single(bay => bay.Id == 2).OccupiedVehicleIds.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportAsync(3, new(2, null, "Over capacity"), Tech));
    }

    [Fact]
    public async Task ScheduleIsNotPhysicalLocation_AndFormerOwnersSeeNothing()
    {
        await using var db = await Seed();
        db.WorkSlots.Add(new WorkSlot { Id = 1, ServiceOrderId = 1, BayId = 1, Status = "SCHEDULED", StartTime = DateTime.Now }); await db.SaveChangesAsync();
        var service = new VehicleLocationService(db);
        Assert.Null((await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1).BayId);
        foreach (var link in await db.CustomerVehicles.Where(link => link.CustomerId == 100).ToListAsync()) link.IsCurrentOwner = false;
        await db.SaveChangesAsync();
        var garage = await service.GetCustomerGarageAsync(100); Assert.Empty(garage.Vehicles); Assert.Empty(garage.Branches);
    }

    [Fact]
    public async Task DeliveredVehicle_DoesNotRemainInGarageDueToOldOccupancyPointer()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        await service.ReportAsync(1, new(1, null, "Arrived"), Tech);
        var order = (await db.ServiceOrders.FindAsync(1L))!; order.Status = "CLOSED"; order.ActualHandover = DateTime.Now.AddDays(2); await db.SaveChangesAsync();
        Assert.Null((await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1).BayId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReportAsync(1, new(2, null, "After delivery"), Tech));
    }

    [Fact]
    public async Task TechnicianMove_DoesNotExposeLegacyLocationWithoutCustomerOrder()
    {
        await using var db = await Seed();
        var source = new SnapshotSource(new([new(1, 1, DateTime.Now.AddDays(-1), null, "Previous owner's note")], new Dictionary<long, int>()));
        var service = new VehicleLocationService(db, source);
        var customer = (await service.GetCustomerGarageAsync(100)).Vehicles.Single(vehicle => vehicle.VehicleId == 1);
        Assert.Null(customer.BayId);
        Assert.Null(customer.Note);
    }

    [Fact]
    public async Task TransferredVehicle_DoesNotExposePreviousOwnersAllocationOrNotes()
    {
        await using var db = await Seed();
        var link = await db.CustomerVehicles.SingleAsync(item => item.CustomerId == 100 && item.VehicleId == 1);
        link.IsCurrentOwner = false;
        db.CustomerVehicles.Add(new CustomerVehicle { Id = 10, CustomerId = 200, VehicleId = 1, IsCurrentOwner = true });
        db.BayAllocations.Add(new BayAllocation { Id = 10, BayId = 1, VehicleId = 1, ServiceOrderId = 1, AllocatedBy = 10, AllocatedAt = DateTime.Now, Notes = "Ghi chú của chủ cũ" });
        (await db.Bays.FindAsync(1L))!.CurrentOccupiedVehicleId = 1;
        await db.SaveChangesAsync();
        var source = new SnapshotSource(new([new(1, 1, DateTime.Now, null, "Thông tin cũ")], new Dictionary<long, int>()));

        var vehicle = (await new VehicleLocationService(db, source).GetCustomerGarageAsync(200)).Vehicles.Single(item => item.VehicleId == 1);
        Assert.Null(vehicle.BayId);
        Assert.Null(vehicle.Note);
    }

    [Fact]
    public async Task NewerManualMove_DoesNotShowOutdatedAllocationToCustomer()
    {
        await using var db = await Seed();
        var reportedAt = DateTime.Now.AddMinutes(-5);
        db.BayAllocations.Add(new BayAllocation { Id = 10, BayId = 1, VehicleId = 1, ServiceOrderId = 1, AllocatedBy = 10, AllocatedAt = reportedAt });
        await db.SaveChangesAsync();
        var source = new SnapshotSource(new([new(2, 1, reportedAt.AddMinutes(1), null, "Moved")], new Dictionary<long, int>()));

        var vehicle = (await new VehicleLocationService(db, source).GetCustomerGarageAsync(100)).Vehicles.Single(item => item.VehicleId == 1);
        Assert.Null(vehicle.BayId);
    }

    [Fact]
    public async Task ClearedBayPointer_DoesNotShowOutdatedAllocationToCustomer()
    {
        await using var db = await Seed();
        var reportedAt = DateTime.Now.AddMinutes(-5);
        db.BayAllocations.Add(new BayAllocation { Id = 10, BayId = 1, VehicleId = 1, ServiceOrderId = 1, AllocatedBy = 10, AllocatedAt = reportedAt });
        var bay = (await db.Bays.FindAsync(1L))!;
        bay.CurrentOccupiedVehicleId = null;
        bay.LastStatusChangedAt = reportedAt.AddMinutes(1);
        await db.SaveChangesAsync();

        var vehicle = (await new VehicleLocationService(db).GetCustomerGarageAsync(100)).Vehicles.Single(item => item.VehicleId == 1);
        Assert.Null(vehicle.BayId);
    }

    [Fact]
    public async Task MaximumLengthReportNote_FitsAuditColumnWithoutLosingCustomerNote()
    {
        await using var db = await Seed(); var service = new VehicleLocationService(db);
        var note = new string('x', 500);
        await service.ReportAsync(1, new(1, null, note), Tech);
        Assert.Equal(note, (await db.BayAllocations.SingleAsync()).Notes);
        Assert.All(await db.BayStatusLogs.ToListAsync(), log => Assert.True(log.Reason!.Length <= 500));
    }

    [Fact]
    public async Task LatestDiagnosisAssignment_RevokesPreviousTechniciansLocationAccess()
    {
        await using var db = await Seed();
        db.ServiceStaffAssignments.RemoveRange(await db.ServiceStaffAssignments.ToListAsync());
        db.Diagnoses.Add(new Diagnosis { Id = 1, ServiceOrderId = 1, Status = "ASSIGNED" });
        db.DiagnosisAssignments.AddRange(new DiagnosisAssignment { Id = 1, DiagnosisId = 1, TechnicianId = 10, EventType = "ASSIGNED", Status = "ASSIGNED" }, new DiagnosisAssignment { Id = 2, DiagnosisId = 1, TechnicianId = 11, EventType = "ASSIGNED", Status = "ASSIGNED" });
        await db.SaveChangesAsync();
        var service = new VehicleLocationService(db);
        Assert.Empty((await service.GetTechnicianWorkspaceAsync(Tech)).Orders);
        Assert.Single((await service.GetTechnicianWorkspaceAsync(Tech with { UserId = 11 })).Orders);
    }

    private sealed class SnapshotSource(BayOccupancySnapshot snapshot) : IBayOccupancySnapshotSource
    {
        public Task<BayOccupancySnapshot> ReadAsync(CancellationToken ct = default) => Task.FromResult(snapshot);
    }

}
