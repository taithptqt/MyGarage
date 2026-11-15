using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class QuotationApprovalPolicyHelper
{
    internal static async Task<QuotationResponseDto> FinancialApproveQuotationAsync(
        IQuotationWorkflowStore store,
        long quotationId,
        QuotationFinancialApprovalRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ApprovedBy <= 0)
        {
            throw new InvalidOperationException("Thiếu người duyệt tài chính.");
        }

        var quotation = await store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (!quotation.RequiresFinancialApproval)
        {
            throw new InvalidOperationException("Báo giá không yêu cầu duyệt tài chính.");
        }

        if (!string.Equals(quotation.FinancialApprovalStatus, QuotationWorkflowConstants.FinancialApprovalPending, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingBranchManager, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Báo giá không ở trạng thái chờ quản lý chi nhánh duyệt.");
        }

        var serviceOrder = await store.GetServiceOrderBranchManagerInfoAsync(quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        if (serviceOrder.BranchManagerId.HasValue && serviceOrder.BranchManagerId.Value > 0 &&
            serviceOrder.BranchManagerId.Value != request.ApprovedBy)
        {
            throw new InvalidOperationException("Chỉ quản lý chi nhánh được phân quyền mới được duyệt tài chính báo giá này.");
        }

        var now = VietnamTime.Now;
        var decision = request.Approved
            ? QuotationWorkflowConstants.FinancialApprovalApproved
            : QuotationWorkflowConstants.FinancialApprovalRejected;
        quotation.FinancialApprovedBy = request.ApprovedBy;
        quotation.FinancialApprovedAt = now;
        quotation.FinancialApprovalStatus = decision;
        quotation.Status = request.Approved
            ? QuotationWorkflowConstants.StatusPendingInsurance
            : QuotationWorkflowConstants.StatusRejected;
        if (request.Approved)
        {
            quotation.RejectReason = null;
        }
        if (!request.Approved)
        {
            quotation.RejectReason = "FINANCIAL_REJECTED";
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var note = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? note
                : $"{quotation.InternalNotes.Trim()} | {note}";
        }

        quotation.UpdatedBy = request.ApprovedBy;
        quotation.UpdatedAt = now;

        store.AddQuotationApprovalAction(new QuotationApprovalAction
        {
            QuotationId = quotation.Id,
            ApprovalType = QuotationWorkflowConstants.ApprovalActionTypeFinancial,
            Decision = decision,
            ActorStaffId = request.ApprovedBy,
            Note = QuotationValidationHelper.NormalizeText(request.Note, 1000),
            CreatedAt = now
        });

        await store.SaveChangesInTransactionAsync();
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    internal static async Task<BranchQuotationApprovalPolicyResponseDto> GetBranchQuotationApprovalPolicyAsync(
        IQuotationWorkflowStore store,
        long branchId)
    {
        if (branchId <= 0)
        {
            throw new InvalidOperationException("Mã chi nhánh không hợp lệ.");
        }

        var row = await store.GetBranchQuotationApprovalPolicyAsNoTrackingAsync(branchId);

        if (row == null)
        {
            return new BranchQuotationApprovalPolicyResponseDto
            {
                BranchId = branchId,
                MaxDiscountPercentWithoutManager = QuotationWorkflowConstants.DefaultMaxDiscountPercentWithoutManager,
                HighValueThreshold = QuotationWorkflowConstants.DefaultHighValueThreshold,
                IsActive = true
            };
        }

        return new BranchQuotationApprovalPolicyResponseDto
        {
            BranchId = row.BranchId,
            MaxDiscountPercentWithoutManager = row.MaxDiscountPercentWithoutManager,
            HighValueThreshold = row.HighValueThreshold,
            IsActive = row.IsActive,
            UpdatedAt = row.UpdatedAt,
            UpdatedBy = row.UpdatedBy
        };
    }

    internal static async Task<BranchQuotationApprovalPolicyResponseDto> UpsertBranchQuotationApprovalPolicyAsync(
        IQuotationWorkflowStore store,
        long branchId,
        BranchQuotationApprovalPolicyUpsertRequestDto request)
    {
        if (branchId <= 0)
        {
            throw new InvalidOperationException("Mã chi nhánh không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu người cập nhật cấu hình duyệt báo giá.");
        }

        var branch = await store.GetBranchInfoAsNoTrackingAsync(branchId);
        if (branch == null)
        {
            throw new InvalidOperationException("Không tìm thấy chi nhánh.");
        }

        if (branch.ManagerId.HasValue && branch.ManagerId.Value > 0 && branch.ManagerId.Value != request.ActorStaffId)
        {
            throw new InvalidOperationException("Chỉ quản lý chi nhánh được phân quyền mới được cập nhật cấu hình duyệt.");
        }

        var maxDiscount = request.MaxDiscountPercentWithoutManager ?? QuotationWorkflowConstants.DefaultMaxDiscountPercentWithoutManager;
        var highValueThreshold = request.HighValueThreshold ?? QuotationWorkflowConstants.DefaultHighValueThreshold;
        if (maxDiscount < 0m || maxDiscount > 100m)
        {
            throw new InvalidOperationException("Ngưỡng chiết khấu phải nằm trong khoảng từ 0 đến 100.");
        }

        if (highValueThreshold < 0m)
        {
            throw new InvalidOperationException("Ngưỡng giá trị báo giá phải lớn hơn hoặc bằng 0.");
        }

        var now = VietnamTime.Now;
        var entity = await store.GetBranchQuotationApprovalPolicyForUpdateAsync(branchId);

        if (entity == null)
        {
            entity = new BranchQuotationApprovalPolicy
            {
                BranchId = branchId,
                MaxDiscountPercentWithoutManager = maxDiscount,
                HighValueThreshold = highValueThreshold,
                IsActive = request.IsActive,
                CreatedBy = request.ActorStaffId,
                CreatedAt = now,
                UpdatedBy = request.ActorStaffId,
                UpdatedAt = now
            };
            store.AddBranchQuotationApprovalPolicy(entity);
        }
        else
        {
            entity.MaxDiscountPercentWithoutManager = maxDiscount;
            entity.HighValueThreshold = highValueThreshold;
            entity.IsActive = request.IsActive;
            entity.UpdatedBy = request.ActorStaffId;
            entity.UpdatedAt = now;
        }

        await store.SaveChangesInTransactionAsync();

        return new BranchQuotationApprovalPolicyResponseDto
        {
            BranchId = entity.BranchId,
            MaxDiscountPercentWithoutManager = entity.MaxDiscountPercentWithoutManager,
            HighValueThreshold = entity.HighValueThreshold,
            IsActive = entity.IsActive,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    internal static async Task<bool> IsTechnicalApprovalRequiredAsync(
        IQuotationWorkflowStore store,
        Quotation quotation)
    {
        if (quotation == null)
        {
            return true;
        }

        var quotationDetails = (quotation.QuotationDetails ?? new List<QuotationDetail>())
            .Where(x => !string.Equals(x.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (quotationDetails.Count == 0)
        {
            return true;
        }

        if (quotationDetails.Any(x => !x.DiagnosisRecommendationId.HasValue || x.DiagnosisRecommendationId.Value <= 0))
        {
            return true;
        }

        var recommendationIds = quotationDetails
            .Select(x => x.DiagnosisRecommendationId.GetValueOrDefault())
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var baselineRecommendations = await store.GetBaselineRecommendationsAsync(recommendationIds);
        if (baselineRecommendations.Count != recommendationIds.Count)
        {
            return true;
        }

        var recommendationById = baselineRecommendations.ToDictionary(x => x.Id);
        foreach (var detail in quotationDetails)
        {
            var recommendationId = detail.DiagnosisRecommendationId.GetValueOrDefault();
            if (!recommendationById.TryGetValue(recommendationId, out var baseline))
            {
                return true;
            }

            var detailItemType = (detail.ItemType ?? string.Empty).Trim().ToUpperInvariant();
            var baselineItemType = (baseline.ItemType ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.Equals(detailItemType, baselineItemType, StringComparison.Ordinal))
            {
                return true;
            }

            var baselineItemId = baseline.ItemId.GetValueOrDefault();
            var detailItemId = detail.ItemId.GetValueOrDefault();
            if (baselineItemId > 0 && detailItemId != baselineItemId)
            {
                return true;
            }

            if (Math.Abs(detail.Quantity - (baseline.Quantity <= 0m ? 1m : baseline.Quantity)) > 0.0001m)
            {
                return true;
            }

            if (detail.IsMandatory != QuotationValidationHelper.IsMandatoryByUrgency(baseline.Urgency))
            {
                return true;
            }
        }

        return false;
    }

    internal static async Task<bool> IsFinancialApprovalRequiredAsync(
        IQuotationWorkflowStore store,
        Quotation quotation)
    {
        if (quotation == null || quotation.ServiceOrderId <= 0)
        {
            return false;
        }

        var branchId = await store.GetServiceOrderExecuteBranchIdAsync(quotation.ServiceOrderId);
        if (!branchId.HasValue || branchId.Value <= 0)
        {
            return false;
        }

        var policy = await store.GetActiveBranchQuotationApprovalPolicyAsNoTrackingAsync(branchId.Value);
        var maxDiscountPercentWithoutManager = policy?.MaxDiscountPercentWithoutManager ?? QuotationWorkflowConstants.DefaultMaxDiscountPercentWithoutManager;
        var highValueThreshold = policy?.HighValueThreshold ?? QuotationWorkflowConstants.DefaultHighValueThreshold;

        var subtotal = Math.Max(0m, quotation.SubTotalAmount);
        var totalDiscount = QuotationPricingHelper.ResolveQuotationTotalDiscountAmount(quotation);
        var discountPercent = subtotal <= 0m
            ? 0m
            : Math.Round((totalDiscount / subtotal) * 100m, 2, MidpointRounding.AwayFromZero);

        if (discountPercent > maxDiscountPercentWithoutManager)
        {
            return true;
        }

        if (highValueThreshold > 0m && quotation.FinalAmount >= highValueThreshold)
        {
            return true;
        }

        return false;
    }
}
