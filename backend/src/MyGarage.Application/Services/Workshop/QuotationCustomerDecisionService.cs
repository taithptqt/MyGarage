using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationCustomerDecisionService : IQuotationCustomerDecisionService
{
    private readonly IQuotationWorkflowStore _store;
    private readonly IServiceOrderDetailService _serviceOrderDetailService;
    private readonly IMaterialRequestService _materialRequestService;
    private readonly IQuotationCustomerPortalService _customerPortalService;
    private readonly IAppLogger<QuotationCustomerDecisionService> _logger;

    public QuotationCustomerDecisionService(
        IQuotationWorkflowStore store,
        IServiceOrderDetailService serviceOrderDetailService,
        IMaterialRequestService materialRequestService,
        IQuotationCustomerPortalService customerPortalService,
        IAppLogger<QuotationCustomerDecisionService> logger)
    {
        _store = store;
        _serviceOrderDetailService = serviceOrderDetailService;
        _materialRequestService = materialRequestService;
        _customerPortalService = customerPortalService;
        _logger = logger;
    }

    public async Task<QuotationResponseDto> ApplyCustomerDecisionAsync(long quotationId, QuotationCustomerDecisionRequestDto request)
    {
        if (request == null)
        {
            throw new InvalidOperationException("Thiếu quyết định của khách hàng.");
        }

        var actorId = request.ActorStaffId
            ?? request.CustomerId
            ?? 0;
        if (actorId <= 0)
        {
            throw new InvalidOperationException("Thiếu người xác nhận quyết định của khách.");
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

        var serviceOrder = await _store.GetServiceOrderWithVehicleForUpdateAsync(quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        QuotationValidationHelper.EnsureCustomerDecisionAllowed(quotation, serviceOrder);

        var now = VietnamTime.Now;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var cleanedNote = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleanedNote
                : $"{quotation.InternalNotes.Trim()} | {cleanedNote}";
        }

        if (!string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingCustomer, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Approved && string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
            {
                return QuotationMappingHelper.MapQuotationResponse(quotation);
            }

            if (!request.Approved && string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCustomerRejected, StringComparison.OrdinalIgnoreCase))
            {
                return QuotationMappingHelper.MapQuotationResponse(quotation);
            }

            throw new InvalidOperationException("Báo giá chưa ở trạng thái chờ khách hàng xác nhận.");
        }

        if (request.Approved)
        {
            var hasPendingMandatoryOverride = await _store.HasPendingMandatoryOverrideAsync(
                quotationId,
                nameof(MandatoryOverrideStatusCode.APPROVED));
            if (hasPendingMandatoryOverride)
            {
                throw new InvalidOperationException("Mandatory override request is pending or rejected.");
            }

            var approvableDetails = quotation.QuotationDetails
                .Where(x => !string.Equals(x.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (approvableDetails.Count == 0)
            {
                throw new InvalidOperationException("Không có hạng mục hợp lệ để xác nhận.");
            }

            foreach (var detail in approvableDetails)
            {
                detail.ApprovalStatus = QuotationWorkflowConstants.ApprovalApproved;
                detail.ApprovedBy = actorId;
                detail.ApprovedAt ??= now;
                detail.RejectReason = null;
                detail.UpdatedAt = now;
                detail.UpdatedBy = actorId;
            }

            quotation.Status = QuotationWorkflowConstants.StatusCustomerApproved;
            quotation.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalApproved;
            quotation.CustomerDecisionBy = actorId;
            quotation.CustomerDecisionAt = now;
            quotation.IsReadyForSplitBilling = true;
            quotation.SplitBillingReadyAt = now;
            quotation.UpdatedAt = now;
            quotation.UpdatedBy = actorId;
            serviceOrder.InsuranceQuoteState = QuotationWorkflowConstants.StatusCustomerApproved;
            await ApplyQuotationToServiceOrderAsync(quotation, serviceOrder, actorId, now);

            // Tự động tạo phiếu yêu cầu vật tư cho các phụ tùng trong báo giá
            try
            {
                await _materialRequestService.CreateFromQuotationAsync(quotationId, actorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tự động tạo phiếu yêu cầu vật tư thất bại cho báo giá bảo hiểm {QuotationId}", quotationId);
                _store.AddAuditLog(new AuditLog
                {
                    UserId = actorId,
                    UserType = "SYSTEM",
                    BranchId = serviceOrder.ExecuteBranchId,
                    Action = "MATERIAL_REQUEST_CREATE_FAILED",
                    TableName = "QUOTATIONS",
                    RecordId = quotation.Id,
                    NewValue = ex.Message,
                    Timestamp = now
                });
                await _store.SaveChangesInTransactionAsync();
            }

            await _customerPortalService.TryNotifyInsuranceApprovalChainAsync(quotation, serviceOrder, true);
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        if (quotation.IsAppliedToServiceOrder)
        {
            throw new InvalidOperationException("Không thể từ chối báo giá đã được áp vào lệnh sửa chữa.");
        }

        quotation.Status = QuotationWorkflowConstants.StatusCustomerRejected;
        quotation.RejectReason = "CUSTOMER_REJECTED";
        quotation.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalRejected;
        quotation.CustomerDecisionBy = actorId;
        quotation.CustomerDecisionAt = now;
        quotation.IsReadyForSplitBilling = false;
        quotation.SplitBillingReadyAt = null;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorId;
        serviceOrder.InsuranceQuoteState = QuotationWorkflowConstants.StatusCustomerRejected;
        serviceOrder.Status = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
        serviceOrder.UpdatedAt = now;
        serviceOrder.UpdatedBy = actorId;

        await _store.SaveChangesInTransactionAsync();
        await _customerPortalService.TryNotifyInsuranceApprovalChainAsync(quotation, serviceOrder, false);
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    private async Task ApplyQuotationToServiceOrderAsync(Quotation quotation, ServiceOrder serviceOrder, long actorId, DateTime now)
    {
        if (quotation.IsAppliedToServiceOrder)
        {
            return;
        }

        var approvedDetails = quotation.QuotationDetails
            .Where(x =>
                !string.Equals(x.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ApprovalStatus, QuotationWorkflowConstants.ApprovalApproved, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (approvedDetails.Count == 0)
        {
            throw new InvalidOperationException("Không có hạng mục được duyệt để áp vào lệnh sửa chữa.");
        }

        var existingServiceOrderDetails = await _store.GetServiceOrderDetailsForUpdateAsync(serviceOrder.Id);
        var isTechnicianSupplementary = await QuotationValidationHelper.IsTechnicianSupplementaryQuotationAsync(_store, quotation, serviceOrder.Id);
        var detailByQuotationDetailId = existingServiceOrderDetails
            .Where(x => x.QuotationDetailId.HasValue && x.QuotationDetailId.Value > 0)
            .GroupBy(x => x.QuotationDetailId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        var rowsToCreate = new List<ServiceOrderDetail>();
        foreach (var approvedDetail in approvedDetails)
        {
            if (detailByQuotationDetailId.ContainsKey(approvedDetail.Id))
            {
                continue;
            }

            ServiceOrderDetail? existingFromSource = null;
            if (approvedDetail.SourceQuotationDetailId.HasValue && approvedDetail.SourceQuotationDetailId.Value > 0)
            {
                detailByQuotationDetailId.TryGetValue(approvedDetail.SourceQuotationDetailId.Value, out existingFromSource);
            }

            if (existingFromSource != null)
            {
                EnsureServiceOrderDetailAdjustable(existingFromSource, approvedDetail.ItemName);
                ApplyApprovedQuotationToExistingServiceOrderDetail(existingFromSource, approvedDetail, quotation, actorId, now);
                detailByQuotationDetailId[approvedDetail.Id] = existingFromSource;
                continue;
            }

            var created = QuotationMappingHelper.BuildServiceOrderDetailFromQuotation(
                approvedDetail,
                quotation,
                serviceOrder.Id,
                actorId,
                now,
                isTechnicianSupplementary);
            rowsToCreate.Add(created);
            detailByQuotationDetailId[approvedDetail.Id] = created;
        }

        var canceledDetails = quotation.QuotationDetails
            .Where(x =>
                string.Equals(x.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                x.SourceQuotationDetailId.HasValue &&
                x.SourceQuotationDetailId.Value > 0)
            .ToList();
        foreach (var canceledDetail in canceledDetails)
        {
            if (!detailByQuotationDetailId.TryGetValue(canceledDetail.SourceQuotationDetailId!.Value, out var existingFromSource))
            {
                continue;
            }

            EnsureServiceOrderDetailAdjustable(existingFromSource, canceledDetail.ItemName);
            existingFromSource.Status = nameof(ServiceOrderDetailStatusCode.CANCELED);
            existingFromSource.Note = MergeCancellationNote(existingFromSource.Note, canceledDetail.CancelReason);
            existingFromSource.QuotationDetailId = canceledDetail.Id;
            existingFromSource.SourceQuotationId = quotation.Id;
            existingFromSource.UpdatedBy = actorId;
            existingFromSource.UpdatedAt = now;
            detailByQuotationDetailId[canceledDetail.Id] = existingFromSource;
        }

        if (rowsToCreate.Count > 0)
        {
            _store.AddServiceOrderDetails(rowsToCreate);
        }

        var existingServiceOrderDiscounts = await _store.GetServiceOrderDiscountsForUpdateAsync(serviceOrder.Id);
        if (existingServiceOrderDiscounts.Count > 0)
        {
            _store.RemoveServiceOrderDiscounts(existingServiceOrderDiscounts);
        }

        var quotationDiscounts = (quotation.QuotationDiscounts ?? new List<QuotationDiscount>()).ToList();
        if (quotationDiscounts.Count > 0)
        {
            var serviceOrderDiscounts = quotationDiscounts.Select(discount => new ServiceOrderDiscount
            {
                ServiceOrderId = serviceOrder.Id,
                DiscountType = discount.DiscountType,
                CustomerTierId = discount.CustomerTierId,
                VoucherId = discount.VoucherId,
                DiscountValue = discount.DiscountValue,
                ValueType = discount.ValueType,
                AppliedAmount = discount.AppliedAmount,
                Reason = discount.Reason,
                AppliedBy = discount.AppliedBy
            }).ToList();
            _store.AddServiceOrderDiscounts(serviceOrderDiscounts);
        }

        quotation.IsAppliedToServiceOrder = true;
        quotation.AppliedBy = actorId;
        quotation.AppliedAt = now;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorId;

        if (serviceOrder.CustomerApprovedAt == null)
        {
            serviceOrder.CustomerApprovedAt = now;
        }

        if (string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.QUOTING), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.WAITING_APPROVAL), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.APPROVED), StringComparison.OrdinalIgnoreCase))
        {
            serviceOrder.Status = nameof(ServiceOrderLifecycleStatus.IN_PROGRESS);
        }

        serviceOrder.UpdatedAt = now;
        serviceOrder.UpdatedBy = actorId;

        await _store.SaveChangesInTransactionAsync();
        await QuotationPricingHelper.RecalculateServiceOrderTotalsAsync(_store, serviceOrder.Id, actorId, now);
        await _serviceOrderDetailService.SyncServiceOrderWaitingPartsStatusAsync(serviceOrder.Id, actorId);
        await _store.SaveChangesInTransactionAsync();
    }

    private static void ApplyApprovedQuotationToExistingServiceOrderDetail(
        ServiceOrderDetail existingDetail,
        QuotationDetail approvedDetail,
        Quotation quotation,
        long actorId,
        DateTime now)
    {
        var normalizedItemType = QuotationMappingHelper.NormalizeServiceOrderDetailItemType(approvedDetail.ItemType);
        var normalizedPayerType = QuotationMappingHelper.NormalizeServiceOrderDetailPayerType(
            approvedDetail.PayerType,
            approvedDetail.InsuranceCoveredAmount,
            approvedDetail.CustomerCoveredAmount);
        var standardServiceId = normalizedItemType == "LABOR" && approvedDetail.ItemId.HasValue && approvedDetail.ItemId.Value > 0
            ? approvedDetail.ItemId
            : null;

        existingDetail.QuotationDetailId = approvedDetail.Id;
        existingDetail.StandardServiceId = standardServiceId;
        existingDetail.ItemType = normalizedItemType;
        existingDetail.ItemId = approvedDetail.ItemId;
        existingDetail.ItemName = approvedDetail.ItemName;
        existingDetail.Description = approvedDetail.Description;
        existingDetail.Quantity = approvedDetail.Quantity <= 0 ? 1m : approvedDetail.Quantity;
        existingDetail.EstimatedMinutes = approvedDetail.EstimatedMinutes;
        existingDetail.CostPriceAtTime = approvedDetail.CostPriceAtTime;
        existingDetail.UnitPriceAtTime = approvedDetail.UnitPriceAtTime;
        existingDetail.SubTotal = approvedDetail.SubTotal;
        existingDetail.DiscountAllocatedAmount = QuotationPricingHelper.ResolveQuotationDetailDiscountAmount(approvedDetail, quotation.QuotationDiscounts);
        existingDetail.TaxRate = approvedDetail.TaxRate;
        existingDetail.TaxAmount = approvedDetail.TaxAmount;
        existingDetail.FinalAmount = approvedDetail.FinalAmount;
        existingDetail.PayerType = normalizedPayerType;
        existingDetail.InsuranceCompanyId = approvedDetail.InsuranceCompanyId;
        existingDetail.InsuranceRequestedAmount = approvedDetail.InsuranceCoveredAmount;
        existingDetail.CustomerCoPayAmount = approvedDetail.CustomerCoveredAmount;
        existingDetail.Status = nameof(ServiceOrderDetailStatusCode.APPROVED);
        existingDetail.IsReturnOldPart = approvedDetail.IsReturnOldPart;
        existingDetail.SourceQuotationId = quotation.Id;
        existingDetail.UpdatedAt = now;
        existingDetail.UpdatedBy = actorId;
    }

    private static void EnsureServiceOrderDetailAdjustable(ServiceOrderDetail detail, string? itemName)
    {
        if (detail == null)
        {
            return;
        }

        if ((detail.IssuedQuantity ?? 0m) > 0m)
        {
            throw new InvalidOperationException(
                $"Không thể giảm/hủy hạng mục '{itemName ?? detail.ItemName}' vì vật tư đã xuất kho.");
        }

        var status = (detail.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (status == nameof(ServiceOrderDetailStatusCode.IN_PROGRESS) ||
            status == nameof(ServiceOrderDetailStatusCode.COMPLETED) ||
            status == nameof(ServiceOrderDetailStatusCode.SETTLED) ||
            status == nameof(ServiceOrderDetailStatusCode.SUBMITTED))
        {
            throw new InvalidOperationException(
                $"Không thể giảm/hủy hạng mục '{itemName ?? detail.ItemName}' vì đã bắt đầu thi công hoặc đã quyết toán.");
        }
    }

    private static string MergeCancellationNote(string? currentNote, string? cancelReason)
    {
        var reason = QuotationValidationHelper.NormalizeText(cancelReason, 500);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return currentNote ?? string.Empty;
        }

        var prefix = "Hủy hạng mục theo báo giá bổ sung: ";
        if (string.IsNullOrWhiteSpace(currentNote))
        {
            return prefix + reason;
        }

        var trimmed = currentNote.Trim();
        if (trimmed.Contains(reason, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed} | {prefix}{reason}";
    }
}
