using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IQuotationCustomerPortalService
{
    Task<QuotationCustomerLinkResponseDto> PublishQuotationCustomerLinkAsync(long quotationId, QuotationCustomerLinkPublishRequestDto request);
    Task<QuotationPublicViewResponseDto> GetQuotationByPublicTokenAsync(string token);
    Task<QuotationPublicOtpResponseDto> SendDecisionOtpByPublicTokenAsync(string token, QuotationPublicOtpRequestDto? request = null);
    Task<(QuotationAccessLink AccessLink, long QuotationId, long? CustomerId)> ValidatePublicDecisionAsync(string token, string? otpCode);
    Task MarkPublicDecisionLinkUsedAsync(QuotationAccessLink accessLink, long quotationId, long? customerId, bool approved);
    Task TryNotifyInsuranceApprovalChainAsync(Quotation quotation, ServiceOrder serviceOrder, bool approved);
    Task NotifyCancellationAlertAsync(Quotation quotation, QuotationDetail canceledDetail, long actorStaffId, DateTime occurredAt);
}
