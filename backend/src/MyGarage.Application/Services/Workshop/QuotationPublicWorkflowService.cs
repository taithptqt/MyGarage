using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class QuotationPublicWorkflowService : IQuotationPublicWorkflowService
{
    private readonly IQuotationCustomerPortalService _customerPortalService;
    private readonly IQuotationCustomerDecisionService _customerDecisionService;
    private readonly IQuotationQueryService _queryService;

    public QuotationPublicWorkflowService(
        IQuotationCustomerPortalService customerPortalService,
        IQuotationCustomerDecisionService customerDecisionService,
        IQuotationQueryService queryService)
    {
        _customerPortalService = customerPortalService;
        _customerDecisionService = customerDecisionService;
        _queryService = queryService;
    }

    public async Task<QuotationResponseDto> SendQuotationToCustomerAsync(long quotationId, long actorStaffId, string? note = null, string? idempotencyKey = null)
    {
        _ = await _customerPortalService.PublishQuotationCustomerLinkAsync(quotationId, new QuotationCustomerLinkPublishRequestDto
        {
            ActorStaffId = actorStaffId,
            Note = note,
            SendEmail = true,
            IdempotencyKey = idempotencyKey
        });

        var updated = await _queryService.GetQuotationAsync(quotationId);
        if (updated == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        return updated;
    }

    public async Task<QuotationResponseDto> ApplyCustomerDecisionByPublicTokenAsync(string token, QuotationPublicDecisionRequestDto request)
    {
        if (request == null)
        {
            throw new InvalidOperationException("Thiếu quyết định của khách hàng.");
        }

        var (accessLink, quotationId, customerId) = await _customerPortalService.ValidatePublicDecisionAsync(token, request.OtpCode);

        var updated = await _customerDecisionService.ApplyCustomerDecisionAsync(quotationId, new QuotationCustomerDecisionRequestDto
        {
            Approved = request.Approved,
            CustomerId = customerId,
            Note = QuotationValidationHelper.NormalizeText(request.Note, 1000)
        });

        await _customerPortalService.MarkPublicDecisionLinkUsedAsync(accessLink, quotationId, customerId, request.Approved);
        return updated;
    }
}
