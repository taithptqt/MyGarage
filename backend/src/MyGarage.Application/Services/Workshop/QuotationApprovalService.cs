using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationApprovalService : IQuotationApprovalService
{
    private readonly IQuotationWorkflowStore _store;

    public QuotationApprovalService(IQuotationWorkflowStore store)
    {
        _store = store;
    }

    public async Task<QuotationResponseDto> ApproveQuotationAsync(long quotationId, QuotationApproveRequestDto request)
    {
        if (request == null || request.ApprovedBy <= 0)
        {
            throw new InvalidOperationException("Thiếu người duyệt báo giá.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế bởi phiên bản mới.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusCustomerApproved && quotation.IsAppliedToServiceOrder)
        {
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        var now = VietnamTime.Now;
        var decisionMap = (request.Decisions ?? new List<QuotationDecisionDto>())
            .Where(x => x != null && x.QuotationDetailId > 0)
            .GroupBy(x => x.QuotationDetailId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var detail in quotation.QuotationDetails)
        {
            if (string.Equals(detail.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (decisionMap.TryGetValue(detail.Id, out var decision))
            {
                if (decision.Approved)
                {
                    detail.ApprovalStatus = QuotationWorkflowConstants.ApprovalApproved;
                    detail.RejectReason = null;
                }
                else
                {
                    detail.ApprovalStatus = QuotationWorkflowConstants.ApprovalRejected;
                    detail.RejectReason = QuotationValidationHelper.NormalizeText(decision.RejectReason, 500) ?? "CUSTOMER_REJECTED";
                }

                detail.ApprovedBy = request.ApprovedBy;
                detail.ApprovedAt = now;
                detail.UpdatedBy = request.ApprovedBy;
                detail.UpdatedAt = now;
            }
            else if (decisionMap.Count == 0)
            {
                detail.ApprovalStatus = QuotationWorkflowConstants.ApprovalApproved;
                detail.RejectReason = null;
                detail.ApprovedBy = request.ApprovedBy;
                detail.ApprovedAt = now;
                detail.UpdatedBy = request.ApprovedBy;
                detail.UpdatedAt = now;
            }
        }

        var approvedDetails = quotation.QuotationDetails
            .Where(x =>
                !string.Equals(x.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ApprovalStatus, QuotationWorkflowConstants.ApprovalApproved, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var technicalApproved = approvedDetails.Count > 0;
        quotation.Status = technicalApproved ? QuotationWorkflowConstants.StatusPendingInsurance : QuotationWorkflowConstants.StatusRejected;
        quotation.ApprovedBy = request.ApprovedBy;
        quotation.ApprovedAt = now;
        quotation.TechnicalApprovedBy = request.ApprovedBy;
        quotation.TechnicalApprovedAt = now;
        quotation.TechnicalApprovalStatus = technicalApproved
            ? QuotationWorkflowConstants.TechnicalApprovalApproved
            : QuotationWorkflowConstants.TechnicalApprovalRejected;
        quotation.RequiresFinancialApproval = false;
        quotation.FinancialApprovalStatus = QuotationWorkflowConstants.FinancialApprovalNotRequired;
        quotation.FinancialApprovedBy = null;
        quotation.FinancialApprovedAt = null;
        quotation.RejectReason = approvedDetails.Count > 0 ? null : "ALL_ITEMS_REJECTED";
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var note = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? note
                : $"{quotation.InternalNotes.Trim()} | {note}";
        }

        quotation.UpdatedBy = request.ApprovedBy;
        quotation.UpdatedAt = now;
        QuotationPricingHelper.RecalculateQuotationTotals(quotation);

        if (technicalApproved)
        {
            var financialApprovalRequired = await QuotationApprovalPolicyHelper.IsFinancialApprovalRequiredAsync(_store, quotation);
            if (financialApprovalRequired)
            {
                quotation.RequiresFinancialApproval = true;
                quotation.FinancialApprovalStatus = QuotationWorkflowConstants.FinancialApprovalPending;
                quotation.Status = QuotationWorkflowConstants.StatusPendingBranchManager;
            }
        }

        _store.AddQuotationApprovalAction(new QuotationApprovalAction
        {
            QuotationId = quotation.Id,
            ApprovalType = QuotationWorkflowConstants.ApprovalActionTypeTechnical,
            Decision = technicalApproved
                ? QuotationWorkflowConstants.TechnicalApprovalApproved
                : QuotationWorkflowConstants.TechnicalApprovalRejected,
            ActorStaffId = request.ApprovedBy,
            Note = QuotationValidationHelper.NormalizeText(request.Note, 1000),
            CreatedAt = now
        });

        await _store.SaveChangesInTransactionAsync();
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> SubmitQuotationForInternalApprovalAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự trình duyệt nội bộ.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusCustomerApproved ||
            quotation.Status == QuotationWorkflowConstants.StatusCustomerRejected ||
            quotation.Status == QuotationWorkflowConstants.StatusCanceled)
        {
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        if (!string.Equals(quotation.Status, QuotationWorkflowConstants.StatusDraft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(quotation.Status, QuotationWorkflowConstants.StatusWaitingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ có thể trình duyệt nội bộ khi báo giá đang ở trạng thái nháp hoặc chờ duyệt.");
        }

        var requiresTechnicalApproval = await QuotationApprovalPolicyHelper.IsTechnicalApprovalRequiredAsync(_store, quotation);
        var requiresFinancialApproval = await QuotationApprovalPolicyHelper.IsFinancialApprovalRequiredAsync(_store, quotation);
        var now = VietnamTime.Now;

        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorStaffId;

        var cleaned = QuotationValidationHelper.NormalizeText(note, 1000);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        quotation.RequiresFinancialApproval = requiresFinancialApproval;
        quotation.FinancialApprovedBy = null;
        quotation.FinancialApprovedAt = null;
        quotation.FinancialApprovalStatus = requiresFinancialApproval
            ? QuotationWorkflowConstants.FinancialApprovalPending
            : QuotationWorkflowConstants.FinancialApprovalNotRequired;

        if (requiresTechnicalApproval)
        {
            quotation.TechnicalApprovedBy = null;
            quotation.TechnicalApprovedAt = null;
            quotation.TechnicalApprovalStatus = QuotationWorkflowConstants.TechnicalApprovalPending;
            quotation.Status = QuotationWorkflowConstants.StatusWaitingApproval;

            await _store.SaveChangesInTransactionAsync();
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        quotation.TechnicalApprovedBy = actorStaffId;
        quotation.TechnicalApprovedAt = now;
        quotation.TechnicalApprovalStatus = QuotationWorkflowConstants.TechnicalApprovalApproved;

        if (requiresFinancialApproval)
        {
            quotation.Status = QuotationWorkflowConstants.StatusPendingBranchManager;
            await _store.SaveChangesInTransactionAsync();
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        quotation.Status = QuotationWorkflowConstants.StatusDraft;
        await _store.SaveChangesInTransactionAsync();
        return await SubmitQuotationAsync(quotationId, actorStaffId, note);
    }

    public async Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự gửi báo giá.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusCustomerApproved ||
            quotation.Status == QuotationWorkflowConstants.StatusCustomerRejected ||
            quotation.Status == QuotationWorkflowConstants.StatusCanceled)
        {
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        var requiresInsuranceApproval = QuotationValidationHelper.IsInsuranceApprovalRequiredForQuotation(quotation);
        var targetStatus = requiresInsuranceApproval
            ? QuotationWorkflowConstants.StatusPendingInsurance
            : QuotationWorkflowConstants.StatusInsuranceApproved;

        if (string.Equals(quotation.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        if (!string.Equals(quotation.Status, QuotationWorkflowConstants.StatusDraft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ có thể gửi báo giá sang trạng thái chờ bảo hiểm từ bản nháp.");
        }

        var technicalStatus = (quotation.TechnicalApprovalStatus ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.Equals(technicalStatus, QuotationWorkflowConstants.TechnicalApprovalApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Báo giá chưa hoàn tất duyệt kỹ thuật nội bộ.");
        }

        if (quotation.RequiresFinancialApproval &&
            !string.Equals(quotation.FinancialApprovalStatus, QuotationWorkflowConstants.FinancialApprovalApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Báo giá chưa hoàn tất duyệt tài chính nội bộ.");
        }

        var now = VietnamTime.Now;
        quotation.Status = targetStatus;
        quotation.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorStaffId;

        var cleaned = QuotationValidationHelper.NormalizeText(note, 1000);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        var serviceOrder = await _store.GetServiceOrderForUpdateAsync(quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = quotation.Status;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = actorStaffId;
        }

        await _store.SaveChangesInTransactionAsync();
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> ConfirmInsuranceApprovedAsync(long quotationId, QuotationInsuranceApprovalRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự xác nhận bảo hiểm.");
        }

        var evidenceUrl = QuotationValidationHelper.NormalizeText(request.InsuranceApprovalEvidenceUrl, 1000);
        if (string.IsNullOrWhiteSpace(evidenceUrl))
        {
            throw new InvalidOperationException("Vui lòng tải lên bằng chứng phê duyệt bảo hiểm trước khi xác nhận.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (string.Equals(quotation.Status, QuotationWorkflowConstants.StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(quotation.InsuranceApprovalEvidenceUrl))
            {
                quotation.InsuranceApprovalEvidenceUrl = evidenceUrl;
                quotation.UpdatedAt = VietnamTime.Now;
                quotation.UpdatedBy = request.ActorStaffId;
                await _store.SaveChangesInTransactionAsync();
            }

            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        if (!string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingInsurance, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ xác nhận bảo hiểm khi báo giá đang ở trạng thái chờ bảo hiểm.");
        }

        quotation.InsuranceApprovalEvidenceUrl = evidenceUrl;
        quotation.Status = QuotationWorkflowConstants.StatusInsuranceApproved;
        quotation.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending;
        var now = VietnamTime.Now;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;

        var cleaned = QuotationValidationHelper.NormalizeText(request.Note, 1000);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        var serviceOrder = await _store.GetServiceOrderForUpdateAsync(quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = QuotationWorkflowConstants.StatusInsuranceApproved;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ActorStaffId;
        }

        await _store.SaveChangesInTransactionAsync();
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    public Task<QuotationResponseDto> FinancialApproveQuotationAsync(long quotationId, QuotationFinancialApprovalRequestDto request)
        => QuotationApprovalPolicyHelper.FinancialApproveQuotationAsync(_store, quotationId, request);

    public Task<BranchQuotationApprovalPolicyResponseDto> GetBranchQuotationApprovalPolicyAsync(long branchId)
        => QuotationApprovalPolicyHelper.GetBranchQuotationApprovalPolicyAsync(_store, branchId);

    public Task<BranchQuotationApprovalPolicyResponseDto> UpsertBranchQuotationApprovalPolicyAsync(long branchId, BranchQuotationApprovalPolicyUpsertRequestDto request)
        => QuotationApprovalPolicyHelper.UpsertBranchQuotationApprovalPolicyAsync(_store, branchId, request);
}
