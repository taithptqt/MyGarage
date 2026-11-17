using MyGarage.Domain.Pricing;
using MyGarage.Models;
using MyGarage.Services;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Interfaces;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationDiscountOverflowCharacterizationTests
{
    [Fact]
    public async Task Clone_PreservesLineDiscountOnItsCorrespondingLine()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceOrders.Add(new ServiceOrder { Id = 190, Code = "SO-190", CustomerId = 100, VehicleId = 1, OriginBranchId = 1, ExecuteBranchId = 1, Status = "DIAGNOSING" });
            db.Diagnoses.Add(new Diagnosis { Id = 191, ServiceOrderId = 190 });
            db.DiagnosisAssignments.Add(new DiagnosisAssignment { Id = 192, DiagnosisId = 191, EventType = "ASSIGNED" });
            db.Quotations.Add(new Quotation { Id = 193, Code = "QT-193", ServiceOrderId = 190, DiagnosisId = 191, DiagnosisAssignmentId = 192, VersionNo = 1, Status = "DRAFT", QuotationType = "REPAIR", InsuranceChangeType = "NONE" });
            db.QuotationDetails.AddRange(
                new QuotationDetail { Id = 194, QuotationId = 193, ServiceOrderId = 190, ItemType = "OTHER", ItemName = "One", Quantity = 1, UnitPriceAtTime = 100, SubTotal = 100, FinalAmount = 90, PayerType = "CUSTOMER", CustomerCoveredAmount = 90, Status = "ACTIVE", ApprovalStatus = "PENDING" },
                new QuotationDetail { Id = 195, QuotationId = 193, ServiceOrderId = 190, ItemType = "OTHER", ItemName = "Two", Quantity = 1, UnitPriceAtTime = 200, SubTotal = 200, FinalAmount = 200, PayerType = "CUSTOMER", CustomerCoveredAmount = 200, Status = "ACTIVE", ApprovalStatus = "PENDING" });
            db.QuotationDiscounts.Add(new QuotationDiscount { Id = 196, QuotationId = 193, QuotationDetailId = 194, DiscountType = "MANUAL", ValueType = "AMOUNT", AppliedAmount = 10, DiscountValue = 10, AppliedBy = 200 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationBuildService>();
        var result = await service.CloneQuotationAsync(193, new QuotationCloneRequestDto { ActorStaffId = 200 });
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var cloned = await db.Quotations.AsNoTracking().Include(q => q.QuotationDetails).Include(q => q.QuotationDiscounts).SingleAsync(q => q.Id == result.Id);
        var line = cloned.QuotationDetails.Single(d => d.SourceQuotationDetailId == 194);
        var discount = Assert.Single(cloned.QuotationDiscounts);
        Assert.Equal(line.Id, discount.QuotationDetailId);
        Assert.Equal(90m, line.FinalAmount);
        Assert.Equal(290m, cloned.FinalAmount);
    }

    [Fact]
    public async Task SplitBilling_RejectsOverflowBeforeChangingTrackedOrStoredRows()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var quotation = new Quotation { Id = 91, Code = "QT-91", Status = "DRAFT", QuotationType = "REPAIR", InsuranceChangeType = "NONE" };
            quotation.QuotationDetails.Add(new QuotationDetail
            {
                Id = 92, ItemType = "SERVICE", ItemName = "Test", Status = "ACTIVE", ApprovalStatus = "PENDING",
                Quantity = 1, UnitPriceAtTime = 100, SubTotal = 100, FinalAmount = 100,
                PayerType = "CUSTOMER", InsuranceCoveredAmount = 0, CustomerCoveredAmount = 100
            });
            quotation.QuotationDiscounts.Add(new QuotationDiscount { Id = 93, DiscountType = "MANUAL", ValueType = "AMOUNT", DiscountValue = 20, AppliedAmount = 20 });
            db.Quotations.Add(quotation);
            return Task.CompletedTask;
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationLineMutationService>();
        var request = new QuotationSplitBillingUpdateRequestDto
        {
            ActorStaffId = 1,
            Items = new() { new() { QuotationDetailId = 92, InsuranceCoveredAmount = 70, DiscountPercentage = 20 } }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateSplitBillingAsync(91, request));
        Assert.False(db.ChangeTracker.HasChanges());
        var detail = await db.QuotationDetails.SingleAsync(x => x.Id == 92);
        Assert.Equal(0m, detail.InsuranceCoveredAmount);
        Assert.Single(await db.QuotationDiscounts.ToListAsync());
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<GarageContext>();
        Assert.Equal(0m, (await verify.QuotationDetails.SingleAsync(x => x.Id == 92)).InsuranceCoveredAmount);
        Assert.Single(await verify.QuotationDiscounts.ToListAsync());
    }

    [Fact]
    public void Recalculate_NewDetailsWithUnassignedIds_KeepTheirOwnDiscounts()
    {
        var first = new QuotationDetail { Quantity = 1, UnitPriceAtTime = 100, PayerType = "CUSTOMER", Status = "ACTIVE" };
        var second = new QuotationDetail { Quantity = 1, UnitPriceAtTime = 200, PayerType = "CUSTOMER", Status = "ACTIVE" };
        var quotation = new Quotation
        {
            QuotationDetails = new List<QuotationDetail> { first, second },
            QuotationDiscounts = new List<QuotationDiscount>
            {
                new() { QuotationDetail = first, DiscountValue = 10, AppliedAmount = 10 },
                new() { QuotationDetail = second, DiscountValue = 30, AppliedAmount = 30 }
            }
        };
        var helper = typeof(QuotationLineMutationService).Assembly.GetType("MyGarage.Services.QuotationPricingHelper")!;
        helper.GetMethod("RecalculateQuotationTotals", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { quotation });
        Assert.Equal(90m, first.FinalAmount);
        Assert.Equal(170m, second.FinalAmount);
        Assert.Equal(260m, quotation.FinalAmount);
    }

    [Theory]
    [InlineData(100, 0, 20)]
    [InlineData(60, 40, 41)]
    public void QuotationLevelDiscountAboveCustomerPortion_IsRejected(decimal insurance, decimal customer, decimal discount)
    {
        var line = new DetailPricingInput(1, 1m, 100m, 0m, 0m, insurance, customer, "SPLIT", "ACTIVE");
        Assert.Throws<InvalidOperationException>(() => QuotationPricingEngine.CalculateTotals([line], discount));
    }

    [Theory]
    [InlineData(100, 0, 0, 100, 0)]
    [InlineData(60, 40, 40, 60, 0)]
    [InlineData(60, 40, 15, 60, 25)]
    [InlineData(0, 100, 100, 0, 0)]
    public void QuotationLevelDiscount_UpToCustomerPortion_PreservesPayerTotals(
        decimal insurance, decimal customer, decimal discount, decimal expectedInsurance, decimal expectedCustomer)
    {
        var line = new DetailPricingInput(1, 1m, 100m, 0m, 0m, insurance, customer, "SPLIT", "ACTIVE");
        var result = QuotationPricingEngine.CalculateTotals([line], discount);
        Assert.Equal(expectedInsurance, result.TotalInsuranceAmount);
        Assert.Equal(expectedCustomer, result.TotalCustomerAmount);
        Assert.Equal(result.FinalAmount, result.TotalInsuranceAmount + result.TotalCustomerAmount);
    }
}
