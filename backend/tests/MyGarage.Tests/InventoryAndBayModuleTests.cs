using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Application.Bays.DTOs;
using MyGarage.Application.Inventory;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Domain.Enums;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses;
using MyGarage.Infrastructure.Inventory;
using MyGarage.Infrastructure.Persistence.Stores.StaffAndBranches;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class InventoryAndBayModuleTests
{
    // ==========================================
    // 1. Pure Inventory Domain Rules Tests
    // ==========================================

    [Fact]
    public void InventoryDomainRules_CalculateAvailableQuantity_AccountsForReservedQuantity()
    {
        // OnHand: 10, Reserved: 4 -> Available: 6
        Assert.Equal(6m, InventoryDomainRules.CalculateAvailableQuantity(10m, 4m));

        // OnHand: 5, Reserved: null -> Available: 5
        Assert.Equal(5m, InventoryDomainRules.CalculateAvailableQuantity(5m, null));

        // OnHand: 5, Reserved: 8 -> Available: 0 (clamp at zero)
        Assert.Equal(0m, InventoryDomainRules.CalculateAvailableQuantity(5m, 8m));
    }

    [Fact]
    public void InventoryDomainRules_CanReserveQuantity_ValidatesThresholdsAndTolerance()
    {
        // OnHand 10, Reserved 3 -> Available 7. Request 5 -> True
        Assert.True(InventoryDomainRules.CanReserveQuantity(10m, 3m, 5m));

        // Request 7 -> True (exact available)
        Assert.True(InventoryDomainRules.CanReserveQuantity(10m, 3m, 7m));

        // Request 7.00005 -> True (within tolerance)
        Assert.True(InventoryDomainRules.CanReserveQuantity(10m, 3m, 7.00005m));

        // Request 8 -> False (exceeds available)
        Assert.False(InventoryDomainRules.CanReserveQuantity(10m, 3m, 8m));

        // Request 0 or negative -> False
        Assert.False(InventoryDomainRules.CanReserveQuantity(10m, 3m, 0m));
        Assert.False(InventoryDomainRules.CanReserveQuantity(10m, 3m, -1m));
    }

    [Fact]
    public void InventoryDomainRules_CalculateReservationDelta_ReturnsExactAvailableIncrement()
    {
        // OnHand: 10, CurrentReserved: 2, RemainingNeeded: 5 -> Delta: 5
        Assert.Equal(5m, InventoryDomainRules.CalculateReservationDelta(10m, 2m, 5m));

        // OnHand: 10, CurrentReserved: 7, RemainingNeeded: 5 -> Available: 3 -> Delta: 3
        Assert.Equal(3m, InventoryDomainRules.CalculateReservationDelta(10m, 7m, 5m));

        // OnHand: 10, CurrentReserved: 10, RemainingNeeded: 5 -> Available: 0 -> Delta: 0
        Assert.Equal(0m, InventoryDomainRules.CalculateReservationDelta(10m, 10m, 5m));
    }

    [Fact]
    public void InventoryDomainRules_CalculateReleaseDelta_ReturnsExactReservedDecrement()
    {
        // CurrentReserved: 5, RemainingToRelease: 3 -> Delta: 3
        Assert.Equal(3m, InventoryDomainRules.CalculateReleaseDelta(5m, 3m));

        // CurrentReserved: 2, RemainingToRelease: 5 -> Delta: 2 (cannot release more than reserved)
        Assert.Equal(2m, InventoryDomainRules.CalculateReleaseDelta(2m, 5m));

        // RemainingToRelease <= 0 -> 0
        Assert.Equal(0m, InventoryDomainRules.CalculateReleaseDelta(5m, 0m));
    }

    [Fact]
    public void InventoryDomainRules_EvaluateMaterialIssuance_BlocksInvalidStatus()
    {
        var inputIssued = new InventoryDomainRules.MaterialIssuanceInput("ISSUED", 10m, 2m, 2m);
        var resIssued = InventoryDomainRules.EvaluateMaterialIssuance(inputIssued, 2m);
        Assert.False(resIssued.CanIssue);
        Assert.Contains("xuất kho hoàn tất", resIssued.BlockingReason);

        var inputRejected = new InventoryDomainRules.MaterialIssuanceInput("REJECTED", 10m, 2m, 2m);
        var resRejected = InventoryDomainRules.EvaluateMaterialIssuance(inputRejected, 2m);
        Assert.False(resRejected.CanIssue);
        Assert.Contains("REJECTED", resRejected.BlockingReason);
    }

    [Fact]
    public void InventoryDomainRules_EvaluateMaterialIssuance_ValidatesQuantities()
    {
        // Quantity <= 0
        var inputZero = new InventoryDomainRules.MaterialIssuanceInput("PENDING", 10m, 0m, 5m);
        var resZero = InventoryDomainRules.EvaluateMaterialIssuance(inputZero, 5m);
        Assert.False(resZero.CanIssue);

        // Quantity > RemainingRequested
        var inputExcess = new InventoryDomainRules.MaterialIssuanceInput("PENDING", 10m, 6m, 5m);
        var resExcess = InventoryDomainRules.EvaluateMaterialIssuance(inputExcess, 5m);
        Assert.False(resExcess.CanIssue);
        Assert.Contains("vượt quá số lượng còn thiếu", resExcess.BlockingReason);

        // OnHand < Quantity
        var inputNoStock = new InventoryDomainRules.MaterialIssuanceInput("PENDING", 3m, 5m, 5m);
        var resNoStock = InventoryDomainRules.EvaluateMaterialIssuance(inputNoStock, 5m);
        Assert.False(resNoStock.CanIssue);
        Assert.Contains("Tồn kho không đủ", resNoStock.BlockingReason);

        // Success: Quantity 4, Reserved 5 -> DeductionFromReserved: 4
        var inputValid = new InventoryDomainRules.MaterialIssuanceInput("PENDING", 10m, 4m, 5m);
        var resValid = InventoryDomainRules.EvaluateMaterialIssuance(inputValid, 5m);
        Assert.True(resValid.CanIssue);
        Assert.Null(resValid.BlockingReason);
        Assert.Equal(4m, resValid.DeductionFromReserved);
    }

    [Fact]
    public void InventoryDomainRules_EvaluateMaterialReturn_GuardsOverReturnAndCondition()
    {
        // Excess return
        var inputExcess = new InventoryDomainRules.MaterialReturnInput(IssuedQuantity: 5m, AlreadyReturnedQuantity: 3m, QuantityToReturn: 3m, ItemCondition: "GOOD");
        var resExcess = InventoryDomainRules.EvaluateMaterialReturn(inputExcess);
        Assert.False(resExcess.CanReturn);
        Assert.Contains("vượt quá số lượng đã xuất có thể trả", resExcess.BlockingReason);

        // Good condition -> ShouldRestock = true
        var inputGood = new InventoryDomainRules.MaterialReturnInput(IssuedQuantity: 5m, AlreadyReturnedQuantity: 1m, QuantityToReturn: 2m, ItemCondition: "GOOD");
        var resGood = InventoryDomainRules.EvaluateMaterialReturn(inputGood);
        Assert.True(resGood.CanReturn);
        Assert.True(resGood.ShouldRestock);

        // Defective condition -> ShouldRestock = false
        var inputDamaged = new InventoryDomainRules.MaterialReturnInput(IssuedQuantity: 5m, AlreadyReturnedQuantity: 1m, QuantityToReturn: 2m, ItemCondition: "DAMAGED");
        var resDamaged = InventoryDomainRules.EvaluateMaterialReturn(inputDamaged);
        Assert.True(resDamaged.CanReturn);
        Assert.False(resDamaged.ShouldRestock);
    }

    [Fact]
    public void InventoryDomainRules_StockAlertsAndSeverity_EvaluatesCorrectly()
    {
        // OutOfStock
        Assert.Equal("OutOfStock", InventoryDomainRules.EvaluateStockAlertType(0m, 10m, 15m));
        Assert.Equal("CRITICAL", InventoryDomainRules.ResolveAlertSeverity("OutOfStock"));

        // BelowMinStock
        Assert.Equal("BelowMinStock", InventoryDomainRules.EvaluateStockAlertType(8m, 10m, 15m));
        Assert.Equal("HIGH", InventoryDomainRules.ResolveAlertSeverity("BelowMinStock"));

        // BelowReorder
        Assert.Equal("BelowReorder", InventoryDomainRules.EvaluateStockAlertType(12m, 10m, 15m));
        Assert.Equal("MEDIUM", InventoryDomainRules.ResolveAlertSeverity("BelowReorder"));

        // Normal (no alert)
        Assert.Null(InventoryDomainRules.EvaluateStockAlertType(20m, 10m, 15m));
    }

    [Fact]
    public void InventoryDomainRules_CalculateMovingAverageCost_CalculatesWeightedCostAccurately()
    {
        // Current: 10 units @ 100,000; Incoming: 10 units @ 120,000 -> MAC: 110,000
        var mac1 = InventoryDomainRules.CalculateMovingAverageCost(10m, 100000m, 10m, 120000m);
        Assert.Equal(110000m, mac1);

        // Current: 0 units; Incoming: 5 units @ 50,000 -> MAC: 50,000
        var mac2 = InventoryDomainRules.CalculateMovingAverageCost(0m, 0m, 5m, 50000m);
        Assert.Equal(50000m, mac2);

        // Current: 20 units @ 80,000; Incoming: 0 units -> MAC: 80,000
        var mac3 = InventoryDomainRules.CalculateMovingAverageCost(20m, 80000m, 0m, 100000m);
        Assert.Equal(80000m, mac3);
    }

    [Fact]
    public void InventoryDomainRules_CalculateTotalInventoryValue_AccountsForUnitCostAndFallback()
    {
        // Quantity 10, MAC 50,000 -> Value: 500,000
        Assert.Equal(500000m, InventoryDomainRules.CalculateTotalInventoryValue(10m, 50000m, 40000m));

        // Quantity 10, MAC null, fallback 40,000 -> Value: 400,000
        Assert.Equal(400000m, InventoryDomainRules.CalculateTotalInventoryValue(10m, null, 40000m));

        // Quantity 0 -> Value: 0
        Assert.Equal(0m, InventoryDomainRules.CalculateTotalInventoryValue(0m, 50000m, 40000m));
    }

    // ==========================================
    // 2. Pure Bay Lifecycle Rules Tests
    // ==========================================

    [Theory]
    [InlineData("ACTIVE", "IN_USE", true)]
    [InlineData("ACTIVE", "UNDER_MAINTENANCE", true)]
    [InlineData("ACTIVE", "BROKEN", true)]
    [InlineData("ACTIVE", "SUSPENDED", true)]
    [InlineData("IN_USE", "ACTIVE", true)]
    [InlineData("IN_USE", "UNDER_MAINTENANCE", true)]
    [InlineData("UNDER_MAINTENANCE", "ACTIVE", true)]
    [InlineData("BROKEN", "UNDER_MAINTENANCE", true)]
    [InlineData("BROKEN", "ACTIVE", true)]
    [InlineData("SUSPENDED", "ACTIVE", true)]
    [InlineData("SUSPENDED", "IN_USE", false)] // Must reactivate before allocating
    [InlineData("AVAILABLE", "INUSE", true)]   // Normalization of aliases
    public void BayLifecycleRules_CanTransitionStatus_EnforcesAllowedTransitions(string currentStatus, string targetStatus, bool expected)
    {
        var result = BayLifecycleRules.CanTransitionStatus(currentStatus, targetStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BayLifecycleRules_ValidateAllocation_EnforcesBranchAndTonnageInvariants()
    {
        // 1. Branch mismatch
        var branchMismatch = new BayLifecycleRules.BayAllocationValidationInput(
            BayBranchId: 1,
            ServiceOrderExecuteBranchId: 2,
            BayOperationalStatus: "ACTIVE",
            IsCurrentlyOccupied: false,
            CurrentOccupiedVehicleId: null,
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r1 = BayLifecycleRules.ValidateAllocation(branchMismatch);
        Assert.False(r1.IsValid);
        Assert.Contains("không thuộc cùng chi nhánh", r1.ErrorMessage);

        // 2. Inactive status
        var inactive = new BayLifecycleRules.BayAllocationValidationInput(
            BayBranchId: 1,
            ServiceOrderExecuteBranchId: 1,
            BayOperationalStatus: "UNDER_MAINTENANCE",
            IsCurrentlyOccupied: false,
            CurrentOccupiedVehicleId: null,
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r2 = BayLifecycleRules.ValidateAllocation(inactive);
        Assert.False(r2.IsValid);
        Assert.Contains("không ở trạng thái sẵn sàng", r2.ErrorMessage);

        // 3. Occupied
        var occupied = new BayLifecycleRules.BayAllocationValidationInput(
            BayBranchId: 1,
            ServiceOrderExecuteBranchId: 1,
            BayOperationalStatus: "ACTIVE",
            IsCurrentlyOccupied: true,
            CurrentOccupiedVehicleId: 100,
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r3 = BayLifecycleRules.ValidateAllocation(occupied);
        Assert.False(r3.IsValid);
        Assert.Contains("phương tiện khác chiếm chỗ", r3.ErrorMessage);

        // 4. Excess tonnage
        var excessTonnage = new BayLifecycleRules.BayAllocationValidationInput(
            BayBranchId: 1,
            ServiceOrderExecuteBranchId: 1,
            BayOperationalStatus: "ACTIVE",
            IsCurrentlyOccupied: false,
            CurrentOccupiedVehicleId: null,
            BayMaxTonnage: 2.0m,
            VehicleTonnage: 3.5m
        );
        var r4 = BayLifecycleRules.ValidateAllocation(excessTonnage);
        Assert.False(r4.IsValid);
        Assert.Contains("vượt quá giới hạn cho phép", r4.ErrorMessage);

        // 5. Eligible allocation succeeds
        var valid = new BayLifecycleRules.BayAllocationValidationInput(
            BayBranchId: 1,
            ServiceOrderExecuteBranchId: 1,
            BayOperationalStatus: "ACTIVE",
            IsCurrentlyOccupied: false,
            CurrentOccupiedVehicleId: null,
            BayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r5 = BayLifecycleRules.ValidateAllocation(valid);
        Assert.True(r5.IsValid);
        Assert.Null(r5.ErrorMessage);
    }

    [Fact]
    public void BayLifecycleRules_ValidateTransfer_GuardsBranchAndTargetReadiness()
    {
        // Branch mismatch
        var branchMismatch = new BayLifecycleRules.BayTransferInput(
            SourceBayBranchId: 1,
            TargetBayBranchId: 2,
            VehicleId: 10,
            SourceCurrentVehicleId: 10,
            TargetBayOperationalStatus: "ACTIVE",
            IsTargetOccupied: false,
            TargetBayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r1 = BayLifecycleRules.ValidateTransfer(branchMismatch);
        Assert.False(r1.IsValid);
        Assert.Contains("khác chi nhánh", r1.ErrorMessage);

        // Target occupied
        var targetOccupied = new BayLifecycleRules.BayTransferInput(
            SourceBayBranchId: 1,
            TargetBayBranchId: 1,
            VehicleId: 10,
            SourceCurrentVehicleId: 10,
            TargetBayOperationalStatus: "ACTIVE",
            IsTargetOccupied: true,
            TargetBayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r2 = BayLifecycleRules.ValidateTransfer(targetOccupied);
        Assert.False(r2.IsValid);
        Assert.Contains("đang bị chiếm chỗ", r2.ErrorMessage);

        // Successful transfer
        var valid = new BayLifecycleRules.BayTransferInput(
            SourceBayBranchId: 1,
            TargetBayBranchId: 1,
            VehicleId: 10,
            SourceCurrentVehicleId: 10,
            TargetBayOperationalStatus: "ACTIVE",
            IsTargetOccupied: false,
            TargetBayMaxTonnage: 3.5m,
            VehicleTonnage: 2.0m
        );
        var r3 = BayLifecycleRules.ValidateTransfer(valid);
        Assert.True(r3.IsValid);
        Assert.Null(r3.ErrorMessage);
    }

    // ==========================================
    // 3. Application Authorization Unit Tests
    // ==========================================

    [Fact]
    public async Task InventoryAuthorizationService_EnforcesBranchScopingAndCustomerDenial()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"AuthTestDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options);
        db.Warehouses.Add(new Warehouse { Id = 101, BranchId = 1, Code = "WH-B1", Name = "Kho B1", Type = "MAIN", IsActive = true, IsDeleted = false });
        db.Warehouses.Add(new Warehouse { Id = 102, BranchId = 2, Code = "WH-B2", Name = "Kho B2", Type = "MAIN", IsActive = true, IsDeleted = false });
        await db.SaveChangesAsync();

        var authService = new InventoryAuthorizationService(new EfInventoryStore(db));

        var staffBranch1 = new UserActorContext(UserId: 200, Role: "Staff", RoleCode: "STAFF", BranchId: 1, IsAdmin: false);
        var admin = new UserActorContext(UserId: 1, Role: "Staff", RoleCode: "ADMIN", BranchId: 1, IsAdmin: true);
        var customer = new UserActorContext(UserId: 50, Role: "Customer", RoleCode: "CUSTOMER", BranchId: null, IsAdmin: false);

        // Staff Branch 1 can access Warehouse 101, cannot access 102
        Assert.True(await authService.CanAccessWarehouseAsync(101, staffBranch1));
        Assert.False(await authService.CanAccessWarehouseAsync(102, staffBranch1));

        // Admin has access to both
        Assert.True(await authService.CanAccessWarehouseAsync(101, admin));
        Assert.True(await authService.CanAccessWarehouseAsync(102, admin));

        // Customer denied
        Assert.False(await authService.CanAccessWarehouseAsync(101, customer));
        Assert.False(await authService.CanManageStockAlertsAsync(1, customer));
        Assert.False(await authService.CanViewStockTransactionsAsync(1, customer));
    }

    [Fact]
    public async Task BayAuthorizationService_EnforcesBranchScopingAndCustomerDenial()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"BayAuthTestDb_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options);
        db.Bays.Add(new Bay { Id = 501, BranchId = 1, Code = "BAY-01", Name = "Khoang 1", ServiceArea = "General", BayType = "Standard", OperationalStatus = "ACTIVE", IsDeleted = false });
        db.Bays.Add(new Bay { Id = 502, BranchId = 2, Code = "BAY-02", Name = "Khoang 2", ServiceArea = "General", BayType = "Standard", OperationalStatus = "ACTIVE", IsDeleted = false });
        await db.SaveChangesAsync();

        var authService = new BayAuthorizationService(new EfBayAuthorizationStore(db));

        var staffBranch1 = new UserActorContext(UserId: 200, Role: "Staff", RoleCode: "STAFF", BranchId: 1, IsAdmin: false);
        var admin = new UserActorContext(UserId: 1, Role: "Staff", RoleCode: "ADMIN", BranchId: null, IsAdmin: true);
        var customer = new UserActorContext(UserId: 50, Role: "Customer", RoleCode: "CUSTOMER", BranchId: null, IsAdmin: false);
        var staffWithoutBranch = new UserActorContext(UserId: 201, Role: "Staff", RoleCode: "STAFF", BranchId: null, IsAdmin: false);
        var staffWithInvalidBranch = new UserActorContext(UserId: 202, Role: "Staff", RoleCode: "STAFF", BranchId: 0, IsAdmin: false);

        // Staff Branch 1 can access Bay 501, cannot access Bay 502
        Assert.True(await authService.CanAccessBayAsync(501, staffBranch1));
        Assert.False(await authService.CanAccessBayAsync(502, staffBranch1));

        // Admin can access both
        Assert.True(await authService.CanAccessBayAsync(501, admin));
        Assert.True(await authService.CanAccessBayAsync(502, admin));

        // Customer denied
        Assert.False(await authService.CanAccessBayAsync(501, customer));
        Assert.False(await authService.CanAllocateBayAsync(501, 0, customer));
        Assert.False(await authService.CanReleaseBayAsync(501, customer));
        Assert.False(await authService.CanAccessBayAsync(501, staffWithoutBranch));
        Assert.False(await authService.CanAccessBranchBaysAsync(1, staffWithoutBranch));
        Assert.False(await authService.CanAllocateBayAsync(501, 123, staffWithoutBranch));
        Assert.False(await authService.CanReleaseBayAsync(501, staffWithoutBranch));
        Assert.False(await authService.CanAccessBayAsync(501, staffWithInvalidBranch));
    }

    [Fact]
    public async Task BayAllocation_StaffWithoutBranch_IsForbidden()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Bays.Add(new Bay { Id = 501, BranchId = 1, Code = "BAY-01", Name = "Khoang 1", ServiceArea = "General", BayType = "Standard", OperationalStatus = "ACTIVE", IsDeleted = false });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "STAFF"));
        var response = await client.PostAsJsonAsync("/api/v1/bay-management/501/allocations", new { BayId = 501, ServiceOrderId = 123, VehicleId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBayApplicationService>();
        var staffWithoutBranch = new UserActorContext(200, "Staff", "STAFF", null, false);
        var admin = new UserActorContext(200, "Staff", "ADMIN", null, true);
        Assert.Empty(await service.GetBaysAsync(null, staffWithoutBranch));
        Assert.Single(await service.GetBaysAsync(null, admin));
    }

    // ==========================================
    // 4. Integration Tests with Dual Routes
    // ==========================================

    [Fact]
    public async Task InventoryCatalog_DualRoutes_SucceedsForStaff()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Products.Add(new Product { Id = 801, Sku = "SKU-CAT-01", Name = "Bugi Denso Iridium", Unit = "Cai", CostPrice = 100000m, RetailPrice = 150000m, IsActive = true, IsDeleted = false });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var staffToken = TestWebApplicationFactory.CreateJwtToken(userId: 200, role: "Staff", roleCode: "STAFF", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Canonical Route (/api/v1/...)
        var resDual = await client.GetAsync("/api/v1/inventory-management/catalog/products?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, resDual.StatusCode);
    }

    [Fact]
    public async Task InventoryPartsPage_DualRoutes_SucceedsForStaff()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Products.Add(new Product { Id = 802, Sku = "SKU-PART-02", Name = "Loc Dau Bosch", Unit = "Cai", CostPrice = 50000m, RetailPrice = 80000m, IsActive = true, IsDeleted = false });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        var staffToken = TestWebApplicationFactory.CreateJwtToken(userId: 200, role: "Staff", roleCode: "STAFF", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Canonical Route (/api/v1/...)
        var resDual = await client.GetAsync("/api/v1/inventory/parts-page?search=Bosch&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, resDual.StatusCode);
    }

    [Fact]
    public async Task BayManagement_AllocateAndRelease_DualRoutes_EnforcesBranchIsolation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.Bays.Add(new Bay
            {
                Id = 701,
                BranchId = 1,
                Code = "BAY-CN1-01",
                Name = "Khoang 1 CN1",
                ServiceArea = "General",
                BayType = "Standard",
                OperationalStatus = "ACTIVE",
                IsDeleted = false
            });

            db.Bays.Add(new Bay
            {
                Id = 702,
                BranchId = 2,
                Code = "BAY-CN2-01",
                Name = "Khoang 1 CN2",
                ServiceArea = "General",
                BayType = "Standard",
                OperationalStatus = "ACTIVE",
                IsDeleted = false
            });

            db.Vehicles.Add(new Vehicle
            {
                Id = 10,
                LicensePlate = "51A-99999",
                ModelId = 1,
                CreatedAt = DateTime.UtcNow
            });

            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 901,
                Code = "SO-CN1-001",
                CustomerId = 100,
                VehicleId = 10,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "IN_PROGRESS",
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        // Staff at Branch 1
        var staffToken = TestWebApplicationFactory.CreateJwtToken(userId: 200, role: "Staff", roleCode: "STAFF", branchId: 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // 1. Staff Branch 1 allocating Bay 702 (Branch 2) receives 403 Forbidden
        var allocPayloadBranch2 = new BayAllocationCreateDto
        {
            BayId = 702,
            ServiceOrderId = 901,
            VehicleId = 10,
            PlannedDuration = 60
        };
        var resForbid = await client.PostAsJsonAsync("/api/v1/bay-management/702/allocations", allocPayloadBranch2);
        Assert.Equal(HttpStatusCode.Forbidden, resForbid.StatusCode);

        // 2. Staff Branch 1 allocating Bay 701 on Primary Route succeeds
        var allocPayloadBranch1 = new BayAllocationCreateDto
        {
            BayId = 701,
            ServiceOrderId = 901,
            VehicleId = 10,
            PlannedDuration = 60
        };
        var resAlloc = await client.PostAsJsonAsync("/api/v1/bay-management/701/allocations", allocPayloadBranch1);
        Assert.Equal(HttpStatusCode.OK, resAlloc.StatusCode);

        var allocResult = await resAlloc.Content.ReadFromJsonAsync<BayAllocationResultDto>();
        Assert.NotNull(allocResult);
        Assert.Equal(701, allocResult.BayId);

        // 3. Timeline query on canonical v1 routes succeeds
        var timelinePrimary = await client.GetAsync("/api/v1/bay-management/timeline?branchId=1");
        Assert.Equal(HttpStatusCode.OK, timelinePrimary.StatusCode);

        var timelineV1 = await client.GetAsync("/api/v1/bays/timeline?branchId=1");
        Assert.Equal(HttpStatusCode.OK, timelineV1.StatusCode);

        // 4. Release Bay on Dual Route succeeds
        var resRelease = await client.DeleteAsync("/api/v1/bays/701/allocations?notes=Finished");
        Assert.Equal(HttpStatusCode.OK, resRelease.StatusCode);

        // 5. Subsequent release fails with BadRequest because bay has no active allocation
        var resReleaseAgain = await client.DeleteAsync("/api/v1/bays/701/allocations");
        Assert.Equal(HttpStatusCode.BadRequest, resReleaseAgain.StatusCode);
    }
}
