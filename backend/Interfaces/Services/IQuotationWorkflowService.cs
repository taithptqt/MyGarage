using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationWorkflowService
{
    Task<QuotationResponseDto> CreateQuotationAsync(long serviceOrderId, QuotationCreateRequestDto request, long? actorStaffId = null);
    Task<QuotationResponseDto?> GetQuotationAsync(long quotationId);
    Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsByServiceOrderAsync(long serviceOrderId);
    Task<QuotationResponseDto> ApproveQuotationAsync(long quotationId, QuotationApproveRequestDto request);
    Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null);
    Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null);
}
