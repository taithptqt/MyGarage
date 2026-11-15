using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationLineMutationService
{
    Task<QuotationResponseDto> CancelQuotationDetailAsync(long quotationId, long quotationDetailId, QuotationDetailCancelRequestDto request);
    Task<QuotationResponseDto> UpdateSplitBillingAsync(long quotationId, QuotationSplitBillingUpdateRequestDto request);
    Task<IReadOnlyList<QuotationDiscountResponseDto>> UpsertQuotationDiscountsAsync(long quotationId, QuotationDiscountUpsertRequestDto request);
    Task DecideMandatoryOverrideAsync(long requestId, MandatoryOverrideDecisionRequestDto request);
    Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null);
}
