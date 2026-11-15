using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IQuotationPortalNotificationService
{
    Task SendQuotationLinkEmailAsync(Quotation quotation, string targetEmail, string publicLink, string? clientNote, DateTime expiresAt);
    Task<QuotationPublicOtpResponseDto> SendDecisionOtpAsync(QuotationAccessLink accessLink, QuotationPublicOtpRequestDto? request = null);
    Task TryNotifyInsuranceApprovalChainAsync(Quotation quotation, ServiceOrder serviceOrder, bool approved);
    Task NotifyCancellationAlertAsync(Quotation quotation, QuotationDetail canceledDetail, long actorStaffId, DateTime occurredAt);
}
