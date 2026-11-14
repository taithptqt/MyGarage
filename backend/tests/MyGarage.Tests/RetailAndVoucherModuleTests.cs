using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Authorization;
using MyGarage.Application.Retail;
using MyGarage.Application.Retail.DTOs;
using MyGarage.Application.Vouchers;
using MyGarage.Application.Vouchers.DTOs;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class RetailAndVoucherModuleTests
{
    // =========================================================================
    // SECTION 1: Pure Domain Rules Unit Tests (RetailOrderLifecycleRules)
    // =========================================================================

    [Fact]
    public void RetailOrderRules_NormalizeStatus_HandlesAliases()
    {
        Assert.Equal("CANCELED", RetailOrderLifecycleRules.NormalizeStatus("CANCELLED"));
        Assert.Equal("CANCELED", RetailOrderLifecycleRules.NormalizeStatus("canceled"));
        Assert.Equal("DRAFT", RetailOrderLifecycleRules.NormalizeStatus("draft"));
        Assert.Equal("CONFIRMED", RetailOrderLifecycleRules.NormalizeStatus("CONFIRMED"));
        Assert.Equal("PROCESSING", RetailOrderLifecycleRules.NormalizeStatus("processing"));
        Assert.Equal("SHIPPING", RetailOrderLifecycleRules.NormalizeStatus("Shipping"));
        Assert.Equal("DELIVERED", RetailOrderLifecycleRules.NormalizeStatus("delivered"));
        Assert.Equal("COMPLETED", RetailOrderLifecycleRules.NormalizeStatus("completed"));
    }

    [Fact]
    public void RetailOrderRules_CanTransition_EnforcesStrictFsm()
    {
        // Valid forward transitions
        Assert.True(RetailOrderLifecycleRules.CanTransition("DRAFT", "CONFIRMED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("DRAFT", "CANCELED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("CONFIRMED", "PROCESSING"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("CONFIRMED", "SHIPPING"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("CONFIRMED", "CANCELED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("PROCESSING", "SHIPPING"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("PROCESSING", "CANCELED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("SHIPPING", "DELIVERED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("SHIPPING", "CANCELED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("DELIVERED", "COMPLETED"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("DELIVERED", "CANCELED"));

        // Self-transition
        Assert.True(RetailOrderLifecycleRules.CanTransition("DRAFT", "DRAFT"));
        Assert.True(RetailOrderLifecycleRules.CanTransition("CONFIRMED", "CONFIRMED"));

        // Invalid transitions
        Assert.False(RetailOrderLifecycleRules.CanTransition("DRAFT", "SHIPPING"));
        Assert.False(RetailOrderLifecycleRules.CanTransition("DRAFT", "DELIVERED"));
        Assert.False(RetailOrderLifecycleRules.CanTransition("DRAFT", "COMPLETED"));
        Assert.False(RetailOrderLifecycleRules.CanTransition("COMPLETED", "CONFIRMED"));
        Assert.False(RetailOrderLifecycleRules.CanTransition("COMPLETED", "CANCELED"));
        Assert.False(RetailOrderLifecycleRules.CanTransition("CANCELED", "CONFIRMED"));
    }

    [Fact]
    public void RetailOrderRules_CanCancel_BlocksTerminalOrders()
    {
        Assert.True(RetailOrderLifecycleRules.CanCancel("DRAFT"));
        Assert.True(RetailOrderLifecycleRules.CanCancel("CONFIRMED"));
        Assert.True(RetailOrderLifecycleRules.CanCancel("PROCESSING"));
        Assert.True(RetailOrderLifecycleRules.CanCancel("SHIPPING"));
        Assert.True(RetailOrderLifecycleRules.CanCancel("DELIVERED"));

        Assert.False(RetailOrderLifecycleRules.CanCancel("COMPLETED"));
        Assert.False(RetailOrderLifecycleRules.CanCancel("CANCELED"));
        Assert.False(RetailOrderLifecycleRules.CanCancel("CANCELLED"));
        Assert.False(RetailOrderLifecycleRules.CanCancel(null));
    }

    [Fact]
    public void RetailOrderRules_StockDeduction_IdempotentAndEvaluatesShortage()
    {
        // 1. Fully deducted already -> idempotent no-op
        var res1 = RetailOrderLifecycleRules.EvaluateStockDeduction(10m, 10m, 50m);
        Assert.False(res1.NeedsDeduction);
        Assert.Equal(0m, res1.QuantityToDeduct);
        Assert.True(res1.CanFulfill);

        // 2. Over-deducted previously -> no-op
        var res2 = RetailOrderLifecycleRules.EvaluateStockDeduction(10m, 12m, 50m);
        Assert.False(res2.NeedsDeduction);
        Assert.Equal(0m, res2.QuantityToDeduct);
        Assert.True(res2.CanFulfill);

        // 3. Partial deduction needed and available
        var res3 = RetailOrderLifecycleRules.EvaluateStockDeduction(10m, 4m, 20m);
        Assert.True(res3.NeedsDeduction);
        Assert.Equal(6m, res3.QuantityToDeduct);
        Assert.True(res3.CanFulfill);

        // 4. Partial deduction needed but insufficient stock
        var res4 = RetailOrderLifecycleRules.EvaluateStockDeduction(10m, 4m, 3m);
        Assert.True(res4.NeedsDeduction);
        Assert.Equal(6m, res4.QuantityToDeduct);
        Assert.False(res4.CanFulfill);
    }

    [Fact]
    public void RetailOrderRules_CalculateLineAndOrderTotals_AppliesAwayFromZeroRounding()
    {
        // Unit price: 125,500.50, Qty: 3, Tax: 10%
        var line = RetailOrderLifecycleRules.CalculateLineAmounts(125_500.50m, 3, 10m);
        Assert.Equal(376_501.50m, line.SubTotal);
        Assert.Equal(37_650.15m, line.TaxAmount);
        Assert.Equal(414_151.65m, line.FinalAmount);

        // Order with voucher discount 50,000
        var totals = RetailOrderLifecycleRules.CalculateOrderTotals(line.SubTotal, line.TaxAmount, 50_000m, 0m);
        Assert.Equal(376_501.50m, totals.SubTotal);
        Assert.Equal(50_000m, totals.TotalDiscount);
        Assert.Equal(37_650.15m, totals.TaxAmount);
        Assert.Equal(364_151.65m, totals.FinalAmount); // 376,501.50 - 50,000 + 37,650.15

        // Discount exceeding subtotal is capped
        var capped = RetailOrderLifecycleRules.CalculateOrderTotals(100_000m, 10_000m, 150_000m, 0m);
        Assert.Equal(150_000m, capped.TotalDiscount);
        Assert.Equal(10_000m, capped.FinalAmount); // 100,000 - 100,000 + 10,000
    }

    [Fact]
    public void RetailOrderRules_ValidateReturnLine_RejectsExcessQuantities()
    {
        // Valid return
        var valid = RetailOrderLifecycleRules.ValidateReturnLine(10m, 2m, 3m);
        Assert.True(valid.IsValid);
        Assert.Null(valid.ErrorMessage);

        // Reject zero or negative
        var zero = RetailOrderLifecycleRules.ValidateReturnLine(10m, 2m, 0m);
        Assert.False(zero.IsValid);

        // Reject exceeding sold quantity
        var excess = RetailOrderLifecycleRules.ValidateReturnLine(10m, 8m, 3m);
        Assert.False(excess.IsValid);
        Assert.Contains("vượt quá số lượng tối đa", excess.ErrorMessage);
    }

    // =========================================================================
    // SECTION 2: Pure Domain Rules Unit Tests (VoucherLifecycleRules)
    // =========================================================================

    [Fact]
    public void VoucherRules_CanTransitionStatus_EnforcesStatusGraph()
    {
        Assert.True(VoucherLifecycleRules.CanTransitionStatus("ACTIVE", "PAUSED"));
        Assert.True(VoucherLifecycleRules.CanTransitionStatus("ACTIVE", "REVOKED"));
        Assert.True(VoucherLifecycleRules.CanTransitionStatus("ACTIVE", "EXHAUSTED"));
        Assert.True(VoucherLifecycleRules.CanTransitionStatus("ACTIVE", "EXPIRED"));
        Assert.True(VoucherLifecycleRules.CanTransitionStatus("PAUSED", "ACTIVE"));
        Assert.True(VoucherLifecycleRules.CanTransitionStatus("EXHAUSTED", "ACTIVE"));

        // Terminal state Archived cannot transition out
        Assert.False(VoucherLifecycleRules.CanTransitionStatus("ARCHIVED", "ACTIVE"));
    }

    [Fact]
    public void VoucherRules_CalculateDiscount_HandlesPercentageFixedAndCaps()
    {
        // Percentage: 10% of 1,000,000 = 100,000
        var p1 = VoucherLifecycleRules.CalculateDiscount("PERCENTAGE", 10m, null, 1_000_000m);
        Assert.Equal(100_000m, p1);

        // Percentage with cap: 20% of 1,000,000 = 200,000 capped at 50,000
        var p2 = VoucherLifecycleRules.CalculateDiscount("PERCENTAGE", 20m, 50_000m, 1_000_000m);
        Assert.Equal(50_000m, p2);

        // Fixed amount: 150,000 on 500,000 spend
        var f1 = VoucherLifecycleRules.CalculateDiscount("FIXED_AMOUNT", 150_000m, null, 500_000m);
        Assert.Equal(150_000m, f1);

        // Fixed amount exceeding eligible spend is capped to spend
        var f2 = VoucherLifecycleRules.CalculateDiscount("FIXED_AMOUNT", 200_000m, null, 80_000m);
        Assert.Equal(80_000m, f2);

        // Zero spend yields zero discount
        var z = VoucherLifecycleRules.CalculateDiscount("PERCENTAGE", 10m, null, 0m);
        Assert.Equal(0m, z);
    }

    [Fact]
    public void VoucherRules_TemporalAndScheduleConstraints_WorkCorrectly()
    {
        var now = new DateTime(2026, 11, 14, 10, 30, 0);

        // Date range
        Assert.True(VoucherLifecycleRules.IsDateRangeValid(now, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));
        Assert.False(VoucherLifecycleRules.IsDateRangeValid(now, new DateTime(2026, 11, 20), new DateTime(2026, 11, 30)));

        // Time of day
        Assert.True(VoucherLifecycleRules.IsTimeOfDayValid(new TimeOnly(10, 30), new TimeOnly(8, 0), new TimeOnly(18, 0)));
        Assert.False(VoucherLifecycleRules.IsTimeOfDayValid(new TimeOnly(20, 0), new TimeOnly(8, 0), new TimeOnly(18, 0)));

        // Day of week: 2026-11-14 is Saturday
        Assert.True(VoucherLifecycleRules.IsDayOfWeekValid(DayOfWeek.Saturday, "1,2,3,4,5,6,0"));
        Assert.True(VoucherLifecycleRules.IsDayOfWeekValid(DayOfWeek.Saturday, "Saturday,Sunday"));
        Assert.False(VoucherLifecycleRules.IsDayOfWeekValid(DayOfWeek.Saturday, "1,2,3,4,5"));
    }

    [Fact]
    public void VoucherRules_BranchWhitelistAndBlacklist_Evaluated()
    {
        // Blacklist blocks branch 2
        Assert.False(VoucherLifecycleRules.IsBranchAllowed(2, null, new[] { 2L, 3L }));
        Assert.True(VoucherLifecycleRules.IsBranchAllowed(1, null, new[] { 2L, 3L }));

        // Whitelist allows only branch 1
        Assert.True(VoucherLifecycleRules.IsBranchAllowed(1, new[] { 1L }, null));
        Assert.False(VoucherLifecycleRules.IsBranchAllowed(2, new[] { 1L }, null));
    }

    [Fact]
    public void VoucherRules_QuotaAndRollbackCalculations_Accurate()
    {
        // Quota check
        Assert.True(VoucherLifecycleRules.CanRedeemTotalQuota(10, 9));
        Assert.False(VoucherLifecycleRules.CanRedeemTotalQuota(10, 10));

        // Customer quota
        Assert.True(VoucherLifecycleRules.CanRedeemCustomerQuota(2, 1));
        Assert.False(VoucherLifecycleRules.CanRedeemCustomerQuota(2, 2));

        // Budget check
        Assert.True(VoucherLifecycleRules.CanRedeemBudget(1_000_000m, 800_000m, 100_000m));
        Assert.False(VoucherLifecycleRules.CanRedeemBudget(1_000_000m, 950_000m, 100_000m));

        // Rollback state calculation
        var rollback = VoucherLifecycleRules.ComputeRollbackState(
            currentUsedCount: 10,
            currentRedeemedCount: 10,
            currentBudgetUsed: 500_000m,
            discountToRollback: 50_000m,
            usageLimit: 10,
            budgetLimit: 1_000_000m,
            now: new DateTime(2026, 11, 14),
            startDate: new DateTime(2026, 11, 1),
            endDate: new DateTime(2026, 11, 30));

        Assert.Equal(9, rollback.NewUsedCount);
        Assert.Equal(9, rollback.NewRedeemedCount);
        Assert.Equal(450_000m, rollback.NewBudgetUsed);
        Assert.Equal(VoucherLifecycleRules.StatusActive, rollback.ResolvedStatus);
        Assert.True(rollback.IsActive);
    }

    // =========================================================================
    // SECTION 3: Authorization Port & Adapter Tests
    // =========================================================================

    [Fact]
    public async Task RetailOrderAuthorizationService_EnforcesOwnershipAndBranchScoping()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"RetailAuthTests_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.RetailOrders.AddRange(
            new RetailOrder { Id = 101, BranchId = 1, CustomerId = 100, OrderCode = "RO-101", SubTotal = 100_000m, FinalAmount = 100_000m, Status = "DRAFT" },
            new RetailOrder { Id = 102, BranchId = 2, CustomerId = 105, OrderCode = "RO-102", SubTotal = 200_000m, FinalAmount = 200_000m, Status = "COMPLETED" }
        );
        db.RetailReturns.Add(new RetailReturn
        {
            Id = 301,
            RetailOrderId = 101,
            ReturnCode = "RET-01",
            Status = "PENDING"
        });
        await db.SaveChangesAsync();

        var authService = new RetailOrderAuthorizationService(db);

        var customer100 = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var customer105 = new UserActorContext(105, "Customer", "CUSTOMER", null, false);
        var staffBranch1 = new UserActorContext(201, "Staff", "STAFF", 1, false);
        var staffBranch2 = new UserActorContext(202, "Staff", "STAFF", 2, false);
        var admin = new UserActorContext(1, "Staff", "ADMIN", null, true);

        // Customer view ownership
        Assert.True(await authService.CanViewRetailOrderAsync(101, customer100));
        Assert.False(await authService.CanViewRetailOrderAsync(102, customer100));
        Assert.True(await authService.CanViewRetailOrderAsync(102, customer105));

        // Staff branch scoping
        Assert.True(await authService.CanViewRetailOrderAsync(101, staffBranch1));
        Assert.False(await authService.CanViewRetailOrderAsync(102, staffBranch1));
        Assert.True(await authService.CanViewRetailOrderAsync(102, staffBranch2));

        // Admin override
        Assert.True(await authService.CanViewRetailOrderAsync(101, admin));
        Assert.True(await authService.CanViewRetailOrderAsync(102, admin));

        // Cancellation rules
        Assert.True(await authService.CanCancelRetailOrderAsync(101, customer100)); // DRAFT is cancellable
        Assert.False(await authService.CanCancelRetailOrderAsync(102, customer105)); // COMPLETED is not cancellable

        // Delete is staff-only
        Assert.False(await authService.CanDeleteRetailOrderAsync(101, customer100));
        Assert.True(await authService.CanDeleteRetailOrderAsync(101, staffBranch1));

        // Return finalization
        Assert.False(await authService.CanFinalizeRetailReturnAsync(301, customer100));
        Assert.True(await authService.CanFinalizeRetailReturnAsync(301, staffBranch1));
        Assert.False(await authService.CanFinalizeRetailReturnAsync(301, staffBranch2));
    }

    [Fact]
    public async Task VoucherAuthorizationService_EnforcesRoleGating()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"VoucherAuthTests_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        var authService = new VoucherAuthorizationService(db);

        var customer = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var staff = new UserActorContext(201, "Staff", "STAFF", 1, false);
        var admin = new UserActorContext(1, "Staff", "ADMIN", null, true);

        // Viewing and validating is open to customer and staff
        Assert.True(await authService.CanViewVouchersAsync(customer));
        Assert.True(await authService.CanValidateVoucherAsync(customer));
        Assert.True(await authService.CanRedeemVoucherAsync(customer));

        // Management operations are staff-only
        Assert.False(await authService.CanCreateVoucherAsync(customer));
        Assert.True(await authService.CanCreateVoucherAsync(staff));
        Assert.False(await authService.CanUpdateVoucherAsync(1, customer));
        Assert.True(await authService.CanUpdateVoucherAsync(1, staff));
        Assert.False(await authService.CanPatchVoucherStatusAsync(1, customer));
        Assert.True(await authService.CanPatchVoucherStatusAsync(1, staff));
        Assert.False(await authService.CanDeleteVoucherAsync(1, customer));
        Assert.True(await authService.CanDeleteVoucherAsync(1, staff));
        Assert.False(await authService.CanReverseRedemptionAsync(1, customer));
        Assert.True(await authService.CanReverseRedemptionAsync(1, staff));
        Assert.False(await authService.CanManageCampaignsAsync(customer));
        Assert.True(await authService.CanManageCampaignsAsync(staff));

        // Approval decision is Admin-only
        Assert.False(await authService.CanDecideApprovalRequestAsync(1, customer));
        Assert.False(await authService.CanDecideApprovalRequestAsync(1, staff));
        Assert.True(await authService.CanDecideApprovalRequestAsync(1, admin));
    }

    // =========================================================================
    // SECTION 4: Application Services & Checkout with Voucher Tests
    // =========================================================================

    [Fact]
    public async Task RetailCheckout_WithoutVoucher_CreatesDraftOrderSuccessfully()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CheckoutNoVoucher_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Branches.Add(new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" });
        db.Warehouses.Add(new Warehouse { Id = 10, BranchId = 1, Code = "WH-R1", Name = "Kho Bán Lẻ", Type = "RETAIL", IsActive = true });
        db.Products.Add(new Product { Id = 1001, Sku = "SKU-OIL-01", Unit = "Chai", Name = "Nhớt Motul 300V", RetailPrice = 350_000m, CostPrice = 280_000m, TaxRate = 10m, IsActive = true });
        db.Inventories.Add(new Inventory { WarehouseId = 10, ProductId = 1001, Quantity = 50m, ReservedQuantity = 0m });
        await db.SaveChangesAsync();

        var authService = new RetailOrderAuthorizationService(db);
        var appService = new RetailOrderApplicationService(db, authService);

        var actor = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var input = new RetailCheckoutInput(
            BranchId: 1,
            CustomerNotes: "Giao trong giờ hành chính",
            VoucherCode: null,
            Items: new List<RetailCheckoutItemInput>
            {
                new(ProductId: 1001, Quantity: 2)
            }
        );

        var result = await appService.CheckoutAsync(input, actor);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.StartsWith("RO", result.OrderCode);
        Assert.Equal(700_000m, result.SubTotal); // 350k * 2
        Assert.Equal(0m, result.VoucherDiscountAmount);
        Assert.Equal(0m, result.TotalDiscountAmount);
        Assert.Equal(70_000m, result.TaxAmount); // 10% of 700k
        Assert.Equal(770_000m, result.FinalAmount);
        Assert.Equal("DRAFT", result.Status);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task RetailCheckout_WithVoucher_CreatesOrderAndRelationalRedemptionLedger()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CheckoutWithVoucher_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Branches.Add(new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" });
        db.Warehouses.Add(new Warehouse { Id = 10, BranchId = 1, Code = "WH-R1", Name = "Kho Bán Lẻ", Type = "SELLABLE", IsActive = true });
        db.Products.Add(new Product { Id = 1001, Sku = "SKU-BUG-01", Unit = "Cái", Name = "Buggy Iridium", RetailPrice = 200_000m, CostPrice = 140_000m, TaxRate = 8m, IsActive = true });
        db.Inventories.Add(new Inventory { WarehouseId = 10, ProductId = 1001, Quantity = 100m, ReservedQuantity = 0m });

        // Seed 10% voucher capped at 50k
        db.Vouchers.Add(new Voucher
        {
            Id = 55,
            Code = "GIAM10",
            VoucherName = "Giảm 10% Tối Đa 50K",
            Type = "PERCENTAGE",
            Value = 10m,
            MaxDiscountAmount = 50_000m,
            MinOrderValue = 300_000m,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(5),
            UsageLimit = 100,
            UsedCount = 5,
            MaxUsesPerCustomer = 2,
            BudgetLimit = 5_000_000m,
            BudgetUsed = 250_000m,
            Status = "ACTIVE",
            IsActive = true,
            Timezone = "Asia/Ho_Chi_Minh",
            Version = new byte[] { 1, 0, 0, 0 }
        });
        await db.SaveChangesAsync();

        var authService = new RetailOrderAuthorizationService(db);
        var appService = new RetailOrderApplicationService(db, authService);

        var actor = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var input = new RetailCheckoutInput(
            BranchId: 1,
            CustomerNotes: "Đơn hàng áp voucher",
            VoucherCode: "GIAM10",
            Items: new List<RetailCheckoutItemInput>
            {
                new(ProductId: 1001, Quantity: 3) // 200k * 3 = 600k subtotal
            }
        );

        var result = await appService.CheckoutAsync(input, actor);

        Assert.NotNull(result);
        Assert.Equal(600_000m, result.SubTotal);
        Assert.Equal(55, result.VoucherId);
        // 10% of 600,000 is 60,000, capped at 50,000
        Assert.Equal(50_000m, result.VoucherDiscountAmount);
        Assert.Equal(50_000m, result.TotalDiscountAmount);
        // Tax 8% of 600,000 is 48,000
        Assert.Equal(48_000m, result.TaxAmount);
        // Final: 600,000 - 50,000 + 48,000 = 598,000
        Assert.Equal(598_000m, result.FinalAmount);

        // Verify relational VoucherRedemption record exists (Relational Single Source of Truth invariant)
        var redemption = await db.VoucherRedemptions
            .FirstOrDefaultAsync(r => r.RetailOrderId == result.Id);

        Assert.NotNull(redemption);
        Assert.Equal(55, redemption.VoucherId);
        Assert.Equal("GIAM10", redemption.VoucherCode);
        Assert.Equal(100, redemption.CustomerId);
        Assert.Equal(50_000m, redemption.DiscountAmount);
        Assert.Equal("REDEEMED", redemption.RedemptionStatus);
        Assert.Equal("VALIDATED", redemption.ValidationStatus);

        // Verify voucher counters updated
        var voucherInDb = await db.Vouchers.FindAsync(55L);
        Assert.NotNull(voucherInDb);
        Assert.Equal(6, voucherInDb.UsedCount);
        Assert.Equal(300_000m, voucherInDb.BudgetUsed);
    }

    [Fact]
    public async Task RetailCheckout_RejectsBelowMinimumSpendVoucher()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"CheckoutMinSpend_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.Branches.Add(new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" });
        db.Warehouses.Add(new Warehouse { Id = 10, BranchId = 1, Code = "WH-R1", Name = "Kho Bán Lẻ", Type = "SELLABLE", IsActive = true });
        db.Products.Add(new Product { Id = 1001, Sku = "SKU-WIP-01", Unit = "Cái", Name = "Cần gạt mưa", RetailPrice = 100_000m, TaxRate = 10m, IsActive = true });
        db.Inventories.Add(new Inventory { WarehouseId = 10, ProductId = 1001, Quantity = 10m });

        // Min spend: 500,000
        db.Vouchers.Add(new Voucher
        {
            Id = 60,
            Code = "VIP500",
            VoucherName = "Giảm 50k cho đơn từ 500k",
            Type = "FIXED_AMOUNT",
            Value = 50_000m,
            MinOrderValue = 500_000m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            Status = "ACTIVE",
            IsActive = true,
            Timezone = "Asia/Ho_Chi_Minh",
            Version = new byte[] { 1, 0, 0, 0 }
        });
        await db.SaveChangesAsync();

        var authService = new RetailOrderAuthorizationService(db);
        var appService = new RetailOrderApplicationService(db, authService);

        var actor = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var input = new RetailCheckoutInput(
            BranchId: 1,
            CustomerNotes: null,
            VoucherCode: "VIP500",
            Items: new List<RetailCheckoutItemInput>
            {
                new(ProductId: 1001, Quantity: 2) // Total 200k < 500k
            }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => appService.CheckoutAsync(input, actor));
        Assert.Contains("tối thiểu", ex.Message);
    }

    // =========================================================================
    // SECTION 5: Dual Route API Endpoints Integration Tests
    // =========================================================================

    [Fact]
    public async Task DualRoutes_AllModule6Controllers_RespondSuccessfully()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.Warehouses.Add(new Warehouse { Id = 10, BranchId = 1, Code = "W1", Name = "W1", Type = "SELLABLE", IsActive = true });
            db.Products.Add(new Product { Id = 101, Sku = "SKU-FLT-01", Unit = "Cái", Name = "Lọc gió", RetailPrice = 150_000m, IsActive = true });
            db.Inventories.Add(new Inventory { WarehouseId = 10, ProductId = 101, Quantity = 20m });

            db.Vouchers.Add(new Voucher
            {
                Id = 1,
                Code = "TESTV1",
                VoucherName = "Test Dual Route Voucher",
                Type = "PERCENTAGE",
                Value = 10m,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(10),
                Status = "ACTIVE",
                IsActive = true,
                Timezone = "Asia/Ho_Chi_Minh",
                Version = new byte[] { 1, 0, 0, 0 }
            });

            db.RetailOrders.Add(new RetailOrder
            {
                Id = 1,
                BranchId = 1,
                CustomerId = 100,
                OrderCode = "ROTEST001",
                SubTotal = 150_000m,
                FinalAmount = 150_000m,
                Status = "DRAFT",
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Staff at Branch 1
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "ADMIN", 1));

        // 1. Retail Orders dual route
        var resRo1 = await client.GetAsync("/api/retail-orders");
        var resRo2 = await client.GetAsync("/api/v1/retail-orders");
        Assert.Equal(HttpStatusCode.OK, resRo1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resRo2.StatusCode);

        // 2. Retail Order Items dual route
        var resRoi1 = await client.GetAsync("/api/retail-order-items");
        var resRoi2 = await client.GetAsync("/api/v1/retail-order-items");
        Assert.Equal(HttpStatusCode.OK, resRoi1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resRoi2.StatusCode);

        // 3. Retail Returns dual route
        var resRet1 = await client.GetAsync("/api/retail-returns");
        var resRet2 = await client.GetAsync("/api/v1/retail-returns");
        Assert.Equal(HttpStatusCode.OK, resRet1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resRet2.StatusCode);

        // 4. Vouchers dual route
        var resVch1 = await client.GetAsync("/api/vouchers");
        var resVch2 = await client.GetAsync("/api/v1/vouchers");
        Assert.Equal(HttpStatusCode.OK, resVch1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resVch2.StatusCode);

        // 5. Voucher Admin List dual route
        var resAdm1 = await client.GetAsync("/api/vouchers/admin-list");
        var resAdm2 = await client.GetAsync("/api/v1/vouchers/admin-list");
        Assert.Equal(HttpStatusCode.OK, resAdm1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resAdm2.StatusCode);

        // 6. Voucher Redemptions dual route
        var resRed1 = await client.GetAsync("/api/voucher-redemptions");
        var resRed2 = await client.GetAsync("/api/v1/voucher-redemptions");
        Assert.Equal(HttpStatusCode.OK, resRed1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resRed2.StatusCode);

        // 7. Voucher Campaigns dual route
        var resCam1 = await client.GetAsync("/api/voucher-campaigns");
        var resCam2 = await client.GetAsync("/api/v1/voucher-campaigns");
        Assert.Equal(HttpStatusCode.OK, resCam1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resCam2.StatusCode);

        // 8. Voucher Approval Requests dual route
        var resApp1 = await client.GetAsync("/api/voucher-approval-requests");
        var resApp2 = await client.GetAsync("/api/v1/voucher-approval-requests");
        Assert.Equal(HttpStatusCode.OK, resApp1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resApp2.StatusCode);

        // 9. Customer Vouchers dual route
        var resCv1 = await client.GetAsync("/api/customer-vouchers");
        var resCv2 = await client.GetAsync("/api/v1/customer-vouchers");
        Assert.Equal(HttpStatusCode.OK, resCv1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resCv2.StatusCode);

        // 10. Direct POST / PUT on Retail Orders returns 405 Method Not Allowed
        var directPost1 = await client.PostAsJsonAsync("/api/retail-orders", new { branchId = 1 });
        var directPost2 = await client.PostAsJsonAsync("/api/v1/retail-orders", new { branchId = 1 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, directPost1.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, directPost2.StatusCode);
    }
}
