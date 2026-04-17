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
    Task<QuotationResponseDto> ApplyCustomerDecisionAsync(long quotationId, QuotationCustomerDecisionRequestDto request);
    Task<QuotationResponseDto> GenerateInitialQuotationFromDiagnosisAsync(long serviceOrderId, long actorStaffId, string? note = null, IReadOnlyList<long>? diagnosisIds = null);
    Task<QuotationResponseDto> CloneQuotationAsync(long quotationId, QuotationCloneRequestDto request);
    Task<QuotationResponseDto> CancelQuotationDetailAsync(long quotationId, long quotationDetailId, QuotationDetailCancelRequestDto request);
    Task<QuotationResponseDto> UpdateSplitBillingAsync(long quotationId, QuotationSplitBillingUpdateRequestDto request);
    Task DecideMandatoryOverrideAsync(long requestId, MandatoryOverrideDecisionRequestDto request);
    Task<IReadOnlyList<MandatoryOverrideRequestResponseDto>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId);
    Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null);
    Task<QuotationResponseDto> ConfirmInsuranceApprovedAsync(long quotationId, QuotationInsuranceApprovalRequestDto request);
    Task<QuotationResponseDto> SendQuotationToCustomerAsync(long quotationId, long actorStaffId, string? note = null, string? idempotencyKey = null);
    Task<QuotationCustomerLinkResponseDto> PublishQuotationCustomerLinkAsync(long quotationId, QuotationCustomerLinkPublishRequestDto request);
    Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null);
    Task<QuotationPublicViewResponseDto> GetQuotationByPublicTokenAsync(string token);
    Task<QuotationPublicOtpResponseDto> SendDecisionOtpByPublicTokenAsync(string token, QuotationPublicOtpRequestDto? request = null);
    Task<QuotationResponseDto> ApplyCustomerDecisionByPublicTokenAsync(string token, QuotationPublicDecisionRequestDto request);
}
