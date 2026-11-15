using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationApprovalService
{
    Task<QuotationResponseDto> ApproveQuotationAsync(long quotationId, QuotationApproveRequestDto request);
    Task<QuotationResponseDto> SubmitQuotationForInternalApprovalAsync(long quotationId, long actorStaffId, string? note = null);
    Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null);
    Task<QuotationResponseDto> ConfirmInsuranceApprovedAsync(long quotationId, QuotationInsuranceApprovalRequestDto request);
    Task<QuotationResponseDto> FinancialApproveQuotationAsync(long quotationId, QuotationFinancialApprovalRequestDto request);
    Task<BranchQuotationApprovalPolicyResponseDto> GetBranchQuotationApprovalPolicyAsync(long branchId);
    Task<BranchQuotationApprovalPolicyResponseDto> UpsertBranchQuotationApprovalPolicyAsync(long branchId, BranchQuotationApprovalPolicyUpsertRequestDto request);
}
