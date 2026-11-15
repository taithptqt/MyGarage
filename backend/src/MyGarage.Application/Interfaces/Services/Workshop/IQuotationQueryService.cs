using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationQueryService
{
    Task<QuotationResponseDto?> GetQuotationAsync(long quotationId);
    Task<IReadOnlyList<QuotationDiscountResponseDto>> GetQuotationDiscountsAsync(long quotationId);
    Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsByServiceOrderAsync(long serviceOrderId);
    Task<IReadOnlyList<MandatoryOverrideRequestResponseDto>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId);
}
