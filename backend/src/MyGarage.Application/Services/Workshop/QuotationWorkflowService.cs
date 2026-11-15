using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed record QuotationWorkflowCollaborators(
    IQuotationCustomerPortalService CustomerPortalService,
    IQuotationCustomerDecisionService CustomerDecisionService,
    IQuotationLineMutationService LineMutationService,
    IQuotationPublicWorkflowService PublicWorkflowService);

public sealed class QuotationWorkflowService : IQuotationWorkflowService
{
    private readonly IQuotationBuildService _buildService;
    private readonly IQuotationQueryService _queryService;
    private readonly IQuotationApprovalService _approvalService;
    private readonly IQuotationCustomerPortalService _customerPortalService;
    private readonly IQuotationCustomerDecisionService _customerDecisionService;
    private readonly IQuotationLineMutationService _lineMutationService;
    private readonly IQuotationPublicWorkflowService _publicWorkflowService;

    public QuotationWorkflowService(
        IQuotationBuildService buildService,
        IQuotationQueryService queryService,
        IQuotationApprovalService approvalService,
        QuotationWorkflowCollaborators collaborators)
    {
        _buildService = buildService;
        _queryService = queryService;
        _approvalService = approvalService;
        _customerPortalService = collaborators.CustomerPortalService;
        _customerDecisionService = collaborators.CustomerDecisionService;
        _lineMutationService = collaborators.LineMutationService;
        _publicWorkflowService = collaborators.PublicWorkflowService;
    }

    public Task<QuotationResponseDto> CreateQuotationAsync(long serviceOrderId, QuotationCreateRequestDto request, long? actorStaffId = null)
        => _buildService.CreateQuotationAsync(serviceOrderId, request, actorStaffId);

    public Task<QuotationResponseDto> GenerateInitialQuotationFromDiagnosisAsync(long serviceOrderId, long actorStaffId, string? note = null, IReadOnlyList<long>? diagnosisIds = null)
        => _buildService.GenerateInitialQuotationFromDiagnosisAsync(serviceOrderId, actorStaffId, note, diagnosisIds);

    public Task<string?> ValidateInitialQuotationFromDiagnosisAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIds = null)
        => _buildService.ValidateInitialQuotationFromDiagnosisAsync(serviceOrderId, diagnosisIds);

    public Task<QuotationResponseDto> CloneQuotationAsync(long quotationId, QuotationCloneRequestDto request)
        => _buildService.CloneQuotationAsync(quotationId, request);

    public Task<QuotationResponseDto?> GetQuotationAsync(long quotationId)
        => _queryService.GetQuotationAsync(quotationId);

    public Task<IReadOnlyList<QuotationDiscountResponseDto>> GetQuotationDiscountsAsync(long quotationId)
        => _queryService.GetQuotationDiscountsAsync(quotationId);

    public Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsByServiceOrderAsync(long serviceOrderId)
        => _queryService.GetQuotationsByServiceOrderAsync(serviceOrderId);

    public Task<IReadOnlyList<MandatoryOverrideRequestResponseDto>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId)
        => _queryService.GetMandatoryOverrideRequestsByServiceOrderAsync(serviceOrderId);

    public Task<QuotationResponseDto> ApproveQuotationAsync(long quotationId, QuotationApproveRequestDto request)
        => _approvalService.ApproveQuotationAsync(quotationId, request);

    public Task<QuotationResponseDto> SubmitQuotationForInternalApprovalAsync(long quotationId, long actorStaffId, string? note = null)
        => _approvalService.SubmitQuotationForInternalApprovalAsync(quotationId, actorStaffId, note);

    public Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null)
        => _approvalService.SubmitQuotationAsync(quotationId, actorStaffId, note);

    public Task<QuotationResponseDto> ConfirmInsuranceApprovedAsync(long quotationId, QuotationInsuranceApprovalRequestDto request)
        => _approvalService.ConfirmInsuranceApprovedAsync(quotationId, request);

    public Task<QuotationResponseDto> FinancialApproveQuotationAsync(long quotationId, QuotationFinancialApprovalRequestDto request)
        => _approvalService.FinancialApproveQuotationAsync(quotationId, request);

    public Task<BranchQuotationApprovalPolicyResponseDto> GetBranchQuotationApprovalPolicyAsync(long branchId)
        => _approvalService.GetBranchQuotationApprovalPolicyAsync(branchId);

    public Task<BranchQuotationApprovalPolicyResponseDto> UpsertBranchQuotationApprovalPolicyAsync(long branchId, BranchQuotationApprovalPolicyUpsertRequestDto request)
        => _approvalService.UpsertBranchQuotationApprovalPolicyAsync(branchId, request);

    public Task<QuotationResponseDto> ApplyCustomerDecisionAsync(long quotationId, QuotationCustomerDecisionRequestDto request)
        => _customerDecisionService.ApplyCustomerDecisionAsync(quotationId, request);

    public Task<IReadOnlyList<QuotationDiscountResponseDto>> UpsertQuotationDiscountsAsync(long quotationId, QuotationDiscountUpsertRequestDto request)
        => _lineMutationService.UpsertQuotationDiscountsAsync(quotationId, request);

    public Task<QuotationResponseDto> CancelQuotationDetailAsync(long quotationId, long quotationDetailId, QuotationDetailCancelRequestDto request)
        => _lineMutationService.CancelQuotationDetailAsync(quotationId, quotationDetailId, request);

    public Task<QuotationResponseDto> UpdateSplitBillingAsync(long quotationId, QuotationSplitBillingUpdateRequestDto request)
        => _lineMutationService.UpdateSplitBillingAsync(quotationId, request);

    public Task DecideMandatoryOverrideAsync(long requestId, MandatoryOverrideDecisionRequestDto request)
        => _lineMutationService.DecideMandatoryOverrideAsync(requestId, request);

    public Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null)
        => _lineMutationService.SupersedeQuotationAsync(quotationId, actorStaffId, note);

    public Task<QuotationResponseDto> SendQuotationToCustomerAsync(long quotationId, long actorStaffId, string? note = null, string? idempotencyKey = null)
        => _publicWorkflowService.SendQuotationToCustomerAsync(quotationId, actorStaffId, note, idempotencyKey);

    public Task<QuotationCustomerLinkResponseDto> PublishQuotationCustomerLinkAsync(long quotationId, QuotationCustomerLinkPublishRequestDto request)
        => _customerPortalService.PublishQuotationCustomerLinkAsync(quotationId, request);

    public Task<QuotationPublicViewResponseDto> GetQuotationByPublicTokenAsync(string token)
        => _customerPortalService.GetQuotationByPublicTokenAsync(token);

    public Task<QuotationPublicOtpResponseDto> SendDecisionOtpByPublicTokenAsync(string token, QuotationPublicOtpRequestDto? request = null)
        => _customerPortalService.SendDecisionOtpByPublicTokenAsync(token, request);

    public Task<QuotationResponseDto> ApplyCustomerDecisionByPublicTokenAsync(string token, QuotationPublicDecisionRequestDto request)
        => _publicWorkflowService.ApplyCustomerDecisionByPublicTokenAsync(token, request);
}
