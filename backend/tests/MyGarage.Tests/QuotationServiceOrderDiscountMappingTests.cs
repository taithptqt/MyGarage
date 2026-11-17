using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class QuotationServiceOrderDiscountMappingTests
{
    [Fact]
    public void Mapping_KeepsLineDiscountOnItsServiceOrderDetail_AndOmitsCanceledLine()
    {
        var current = new ServiceOrderDetail { Id = 35 };
        var newDetail = new ServiceOrderDetail();
        var quote = new Quotation
        {
            QuotationDetails = new List<QuotationDetail>
            {
                new() { Id = 10 }, new() { Id = 11 }, new() { Id = 12, Status = "CANCELED" }
            },
            QuotationDiscounts = new List<QuotationDiscount>
            {
                new() { QuotationDetailId = 10, DiscountType = "MANUAL", DiscountCategory = 3 },
                new() { QuotationDetailId = 11, DiscountType = "MANUAL" },
                new() { QuotationDetailId = 12, DiscountType = "MANUAL" },
                new() { DiscountType = "VOUCHER" }
            }
        };

        var result = QuotationCustomerDecisionService.BuildServiceOrderDiscounts(
            quote, 20, new Dictionary<long, ServiceOrderDetail> { [10] = current, [11] = newDetail });

        Assert.Equal(3, result.Count);
        Assert.Same(current, result[0].ServiceOrderDetail);
        Assert.Equal(35, result[0].ServiceOrderDetailId);
        Assert.Equal(3, result[0].DiscountCategory);
        Assert.Same(newDetail, result[1].ServiceOrderDetail);
        Assert.Null(result[1].ServiceOrderDetailId);
        Assert.Null(result[2].ServiceOrderDetail);
        Assert.Null(result[2].ServiceOrderDetailId);
    }

    [Fact]
    public void Mapping_RejectsDiscountForAnotherQuotation()
    {
        var quote = new Quotation
        {
            QuotationDetails = new List<QuotationDetail> { new() { Id = 10 } },
            QuotationDiscounts = new List<QuotationDiscount> { new() { QuotationDetailId = 99, DiscountType = "MANUAL" } }
        };

        Assert.Throws<InvalidOperationException>(() => QuotationCustomerDecisionService.BuildServiceOrderDiscounts(
            quote, 20, new Dictionary<long, ServiceOrderDetail> { [99] = new() { Id = 35 } }));
    }
}
