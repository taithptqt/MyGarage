using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class QuotationDiscountReplacementTests
{
    [Fact]
    public async Task ReplaceQuotationDiscounts_PreservesLineDiscountsAndReturnsOnlyQuotationDiscounts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            var quotation = new Quotation { Id = 91, Code = "QT-91", Status = "DRAFT", QuotationType = "REPAIR", InsuranceChangeType = "NONE" };
            quotation.QuotationDetails.Add(new QuotationDetail { Id = 92, ItemType = "SERVICE", ItemName = "Test", Status = "ACTIVE", ApprovalStatus = "PENDING", Quantity = 1, UnitPriceAtTime = 100, SubTotal = 100, FinalAmount = 90, PayerType = "CUSTOMER", CustomerCoveredAmount = 90 });
            quotation.QuotationDiscounts.Add(new QuotationDiscount { Id = 93, QuotationDetailId = 92, DiscountType = "MANUAL", ValueType = "AMOUNT", DiscountValue = 10, AppliedAmount = 10 });
            quotation.QuotationDiscounts.Add(new QuotationDiscount { Id = 94, DiscountType = "MEMBERSHIP", ValueType = "AMOUNT", DiscountValue = 5, AppliedAmount = 5 });
            db.Quotations.Add(quotation);
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuotationLineMutationService>();
        var rows = await service.UpsertQuotationDiscountsAsync(91, new QuotationDiscountUpsertRequestDto
        {
            ActorStaffId = 1,
            Items = new() { new() { DiscountType = "MANUAL", ValueType = "AMOUNT", DiscountValue = 20, AppliedAmount = 20 } }
        });
        Assert.Single(rows);
        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<GarageContext>();
        var quote = await db.Quotations.AsNoTracking().Include(x => x.QuotationDetails).Include(x => x.QuotationDiscounts).SingleAsync(x => x.Id == 91);
        Assert.Equal(2, quote.QuotationDiscounts.Count);
        Assert.Contains(quote.QuotationDiscounts, x => x.QuotationDetailId == 92 && x.AppliedAmount == 10);
        Assert.Contains(quote.QuotationDiscounts, x => x.QuotationDetailId == null && x.AppliedAmount == 20);
        Assert.Equal(90, quote.QuotationDetails.Single().FinalAmount);
        Assert.Equal(70, quote.FinalAmount);
        var query = verifyScope.ServiceProvider.GetRequiredService<IQuotationQueryService>();
        var queried = await query.GetQuotationDiscountsAsync(91);
        Assert.Single(queried);
        Assert.Equal(20, queried[0].AppliedAmount);
    }
}