using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationWorkflowService :
    IQuotationBuildService,
    IQuotationQueryService,
    IQuotationApprovalService,
    IQuotationCustomerDecisionService,
    IQuotationLineMutationService,
    IQuotationPublicWorkflowService
{
    Task<QuotationCustomerLinkResponseDto> PublishQuotationCustomerLinkAsync(long quotationId, QuotationCustomerLinkPublishRequestDto request);
    Task<QuotationPublicViewResponseDto> GetQuotationByPublicTokenAsync(string token);
    Task<QuotationPublicOtpResponseDto> SendDecisionOtpByPublicTokenAsync(string token, QuotationPublicOtpRequestDto? request = null);
}
