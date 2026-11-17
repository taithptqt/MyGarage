using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationDecisionSafetyTests
{
    [Fact]
    public async Task ApplyDecision_WithExistingUnattributedDiscount_IsRejectedWithoutDeletingOrApproving()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.VehicleBrands.Add(new VehicleBrand { Id = 1, Code = "B1", Name = "Brand" });
            db.VehicleModels.Add(new VehicleModel { Id = 1, BrandId = 1, ModelName = "Model" });
            db.Vehicles.Add(new Vehicle { Id = 1, ModelId = 1, LicensePlate = "TEST-1" });
            db.ServiceOrders.Add(new ServiceOrder { Id = 810, Code = "SO-810", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "QUOTING", FinalAmount = 90 });
            db.Quotations.Add(new Quotation { Id = 811, Code = "QT-811", ServiceOrderId = 810, VersionNo = 2, Status = "PENDING_CUSTOMER", QuotationType = "REPAIR", InsuranceChangeType = "NONE" });
            db.QuotationDetails.Add(new QuotationDetail { Id = 812, QuotationId = 811, ServiceOrderId = 810, ItemType = "OTHER", ItemName = "Work", Quantity = 1, UnitPriceAtTime = 100, SubTotal = 100, FinalAmount = 100, PayerType = "CUSTOMER", CustomerCoveredAmount = 100, Status = "ACTIVE", ApprovalStatus = "PENDING" });
            db.ServiceOrderDiscounts.Add(new ServiceOrderDiscount { Id = 813, ServiceOrderId = 810, DiscountType = "MANUAL", ValueType = "AMOUNT", DiscountValue = 10, AppliedAmount = 10, AppliedBy = 200 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationCustomerDecisionService>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyCustomerDecisionAsync(811, new QuotationCustomerDecisionRequestDto { Approved = true, CustomerId = 100 }));
        Assert.Contains("đối soát", error.Message);
        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal("PENDING_CUSTOMER", (await db.Quotations.AsNoTracking().SingleAsync(x => x.Id == 811)).Status);
        Assert.Equal(10, (await db.ServiceOrderDiscounts.AsNoTracking().SingleAsync(x => x.Id == 813)).AppliedAmount);
        Assert.Empty(await db.ServiceOrderDetails.AsNoTracking().Where(x => x.ServiceOrderId == 810).ToListAsync());
        Assert.Equal(90, (await db.ServiceOrders.AsNoTracking().SingleAsync(x => x.Id == 810)).FinalAmount);
    }

    [Fact]
    public async Task RecalculateOrderTotals_SubtractsOnlyOrderLevelDiscountOnce()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.ServiceOrders.Add(new ServiceOrder { Id = 820, Code = "SO-820", Status = "QUOTING" });
            db.ServiceOrderDetails.Add(new ServiceOrderDetail { Id = 821, ServiceOrderId = 820, ItemType = "LABOR", ItemName = "Work", Unit = "job", PayerType = "CUSTOMER", ServiceSource = "INITIAL", Quantity = 1, SubTotal = 100, TaxAmount = 0, FinalAmount = 90, Status = "APPROVED" });
            db.ServiceOrderDiscounts.AddRange(
                new ServiceOrderDiscount { Id = 822, ServiceOrderId = 820, ServiceOrderDetailId = 821, DiscountType = "MANUAL", ValueType = "AMOUNT", DiscountValue = 10, AppliedAmount = 10 },
                new ServiceOrderDiscount { Id = 823, ServiceOrderId = 820, DiscountType = "VOUCHER", ValueType = "AMOUNT", DiscountValue = 20, AppliedAmount = 20 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IQuotationWorkflowStore>();
        await QuotationPricingHelper.RecalculateServiceOrderTotalsAsync(store, 820, 200, DateTime.UtcNow);
        await store.SaveChangesInTransactionAsync();
        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal(70, (await db.ServiceOrders.AsNoTracking().SingleAsync(x => x.Id == 820)).FinalAmount);
    }

    [Fact]
    public async Task PriorOpenQuotations_ExcludeRejectedAndCanceledHistory()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.Quotations.AddRange(
                new Quotation { Id = 831, ServiceOrderId = 830, Code = "QT-831", Status = "DRAFT", QuotationType = "REPAIR", InsuranceChangeType = "NONE" },
                new Quotation { Id = 832, ServiceOrderId = 830, Code = "QT-832", Status = "CUSTOMER_REJECTED", QuotationType = "REPAIR", InsuranceChangeType = "NONE" },
                new Quotation { Id = 833, ServiceOrderId = 830, Code = "QT-833", Status = "CANCELED", QuotationType = "REPAIR", InsuranceChangeType = "NONE" },
                new Quotation { Id = 834, ServiceOrderId = 830, Code = "QT-834", Status = "CUSTOMER_APPROVED", QuotationType = "REPAIR", InsuranceChangeType = "NONE" });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IQuotationWorkflowStore>();
        var rows = await store.GetPriorOpenQuotationsForUpdateAsync(830, "SUPERSEDED", "CUSTOMER_APPROVED");
        Assert.Equal(831, Assert.Single(rows).Id);
    }
}