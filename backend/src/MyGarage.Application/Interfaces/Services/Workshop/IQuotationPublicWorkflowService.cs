using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationPublicWorkflowService
{
    Task<QuotationResponseDto> SendQuotationToCustomerAsync(long quotationId, long actorStaffId, string? note = null, string? idempotencyKey = null);
    Task<QuotationResponseDto> ApplyCustomerDecisionByPublicTokenAsync(string token, QuotationPublicDecisionRequestDto request);
}
