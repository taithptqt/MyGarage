using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class ChatbotVoucherResolverServiceTests
{
    [Fact]
    public async Task ResolveBestVoucherAsync_ExpiredVoucher_ReturnsNone()
    {
        var now = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var ctx = CreateContext(nameof(ResolveBestVoucherAsync_ExpiredVoucher_ReturnsNone));
        SeedBase(ctx);
        SeedVoucher(ctx, now, id: 1001, code: "V-EXPIRED", start: now.AddDays(-10), end: now.AddDays(-1));

        var sut = CreateSut(ctx, now);
        var result = await sut.ResolveBestVoucherAsync(BuildRetailRequest());

        Assert.False(result.IsApplied);
        Assert.Null(result.Voucher);
    }

    [Fact]
    public async Task ResolveBestVoucherAsync_UsageLimitReached_ReturnsNone()
    {
        var now = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var ctx = CreateContext(nameof(ResolveBestVoucherAsync_UsageLimitReached_ReturnsNone));
        SeedBase(ctx);
        SeedVoucher(
            ctx,
            now,
            id: 1002,
            code: "V-USAGE-LIMIT",
            start: now.AddDays(-1),
            end: now.AddDays(7),
            usageLimit: 1,
            usedCount: 1);

        var sut = CreateSut(ctx, now);
        var result = await sut.ResolveBestVoucherAsync(BuildRetailRequest());

        Assert.False(result.IsApplied);
        Assert.Null(result.Voucher);
    }

    [Fact]
    public async Task ResolveBestVoucherAsync_BudgetExceeded_ReturnsNone()
    {
        var now = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var ctx = CreateContext(nameof(ResolveBestVoucherAsync_BudgetExceeded_ReturnsNone));
        SeedBase(ctx);
        SeedVoucher(
            ctx,
            now,
            id: 1003,
            code: "V-BUDGET",
            start: now.AddDays(-1),
            end: now.AddDays(7),
            value: 10m,
            budgetLimit: 15m,
            budgetUsed: 10m);

        var sut = CreateSut(ctx, now);
        var result = await sut.ResolveBestVoucherAsync(BuildRetailRequest(eligibleAmount: 8m));

        Assert.False(result.IsApplied);
        Assert.Null(result.Voucher);
    }

    [Fact]
    public async Task ResolveBestVoucherAsync_ConflictWithTierDiscount_ReturnsNone()
    {
        var now = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var ctx = CreateContext(nameof(ResolveBestVoucherAsync_ConflictWithTierDiscount_ReturnsNone));
        SeedBase(ctx);
        SeedVoucher(
            ctx,
            now,
            id: 1004,
            code: "V-TIER-CONFLICT",
            start: now.AddDays(-1),
            end: now.AddDays(7),
            isStackable: true);

        ctx.VoucherConflictRules.Add(new VoucherConflictRule
        {
            VoucherId = 1004,
            ConflictType = "TIER_DISCOUNT",
            ConflictTarget = "MEMBERSHIP",
            ActionType = "BLOCK",
            IsActive = true,
            Priority = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        ctx.SaveChanges();

        var sut = CreateSut(ctx, now);
        var result = await sut.ResolveBestVoucherAsync(BuildRetailRequest(tierDiscountAmount: 20m));

        Assert.False(result.IsApplied);
        Assert.Null(result.Voucher);
    }

    [Fact]
    public async Task ResolveBestVoucherAsync_ValidVoucherNearlyExpiring_ReturnsBestVoucher()
    {
        var now = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var ctx = CreateContext(nameof(ResolveBestVoucherAsync_ValidVoucherNearlyExpiring_ReturnsBestVoucher));
        SeedBase(ctx);
        SeedVoucher(
            ctx,
            now,
            id: 1005,
            code: "V-SOON",
            start: now.AddDays(-1),
            end: now.AddHours(5),
            value: 10m);
        SeedVoucher(
            ctx,
            now,
            id: 1006,
            code: "V-LATER",
            start: now.AddDays(-1),
            end: now.AddDays(5),
            value: 10m);

        var sut = CreateSut(ctx, now);
        var result = await sut.ResolveBestVoucherAsync(BuildRetailRequest(eligibleAmount: 100m));

        Assert.True(result.IsApplied);
        Assert.NotNull(result.Voucher);
        Assert.Equal(1005, result.Voucher!.VoucherId);
        Assert.Equal("V-SOON", result.Voucher.VoucherCode);
    }

    private static ChatbotVoucherResolverService CreateSut(GarageContext ctx, DateTime nowUtc)
        => new(
            ctx,
            new FakeClock(nowUtc),
            NullLogger<ChatbotVoucherResolverService>.Instance);

    private static ChatbotVoucherResolveRequest BuildRetailRequest(decimal eligibleAmount = 100m, decimal tierDiscountAmount = 0m)
        => new()
        {
            CustomerId = 101,
            BranchId = 501,
            EligibleAmount = eligibleAmount,
            TierDiscountAmount = tierDiscountAmount,
            OrderType = ChatbotVoucherOrderType.RETAIL_ORDER_DRAFT,
            ProductIds = [701]
        };

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static void SeedBase(GarageContext ctx)
    {
        ctx.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Phone = "0900000000",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        ctx.Branches.Add(new Branch
        {
            Id = 501,
            Name = "Branch A",
            Status = "ACTIVE",
            IsDeleted = false
        });
        ctx.Products.Add(new Product
        {
            Id = 701,
            Sku = "P-701",
            Name = "Engine Oil",
            Unit = "BOTTLE",
            RetailPrice = 100m,
            TaxRate = 10m,
            IsActive = true,
            IsDeleted = false
        });
        ctx.SaveChanges();
    }

    private static void SeedVoucher(
        GarageContext ctx,
        DateTime now,
        long id,
        string code,
        DateTime start,
        DateTime end,
        decimal value = 10m,
        int? usageLimit = null,
        int? usedCount = null,
        decimal? budgetLimit = null,
        decimal budgetUsed = 0m,
        bool isStackable = false)
    {
        ctx.Vouchers.Add(new Voucher
        {
            Id = id,
            Code = code,
            VoucherName = code,
            Type = "FIXED_AMOUNT",
            Value = value,
            MinOrderValue = 0m,
            StartDate = start,
            EndDate = end,
            UsageLimit = usageLimit ?? 0,
            UsedCount = usedCount ?? 0,
            IsStackable = isStackable,
            IsActive = true,
            IsDeleted = false,
            Status = nameof(VoucherStatusCode.ACTIVE),
            MaxUsesPerCustomer = 0,
            MaxUsesPerVehicle = 0,
            IssuedCount = 1,
            RedeemedCount = 0,
            BudgetLimit = budgetLimit,
            BudgetUsed = budgetUsed,
            FirstUseOnly = false,
            ExcludeDiscountedItems = false,
            Timezone = "Asia/Ho_Chi_Minh",
            ApprovalRequired = false,
            Version = [1]
        });

        ctx.VoucherAssignments.Add(new VoucherAssignment
        {
            VoucherId = id,
            AssignmentType = "CUSTOMER",
            TargetId = 101,
            IsInclude = true,
            EffectiveFrom = now.AddDays(-2),
            EffectiveTo = now.AddDays(2),
            CreatedAt = now,
            UpdatedAt = now
        });

        ctx.VoucherUsageRules.Add(new VoucherUsageRule
        {
            VoucherId = id,
            RuleType = "BOOKING_CHANNEL",
            Operator = "IN",
            RuleValue = "RETAIL_ORDER",
            IsExclude = false,
            HardBlock = true,
            IsActive = true,
            Priority = 1,
            CreatedAt = now,
            UpdatedAt = now
        });

        ctx.VoucherUsageRules.Add(new VoucherUsageRule
        {
            VoucherId = id,
            RuleType = "PRODUCT",
            Operator = "IN",
            RuleValue = "701",
            IsExclude = false,
            HardBlock = true,
            IsActive = true,
            Priority = 2,
            CreatedAt = now,
            UpdatedAt = now
        });

        ctx.SaveChanges();
    }

    private sealed class FakeClock : ISystemClock
    {
        private readonly DateTime _utcNow;

        public FakeClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;

        public DateTime LocalNow => _utcNow;

        public DateOnly LocalToday => DateOnly.FromDateTime(_utcNow);
    }
}
