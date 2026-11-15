using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationApprovalDiscountStore
{
    Task<Quotation?> GetQuotationWithDetailsAndDiscountsAsync(long quotationId);
    Task<bool> QuotationExistsAsync(long quotationId);
    Task<List<QuotationDiscount>> GetQuotationDiscountsAsNoTrackingAsync(long quotationId);
    void RemoveQuotationDiscounts(IEnumerable<QuotationDiscount> rows);
    void AddQuotationDiscounts(IEnumerable<QuotationDiscount> rows);
    void AddQuotationDiscount(QuotationDiscount row);
    Task<List<Quotation>> GetQuotationsByServiceOrderWithDetailsAndDiscountsAsync(long serviceOrderId);
}
