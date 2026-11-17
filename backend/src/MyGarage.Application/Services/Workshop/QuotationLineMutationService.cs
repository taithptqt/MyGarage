using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationLineMutationService : IQuotationLineMutationService
{
    private readonly IQuotationWorkflowStore _store;
    private readonly IQuotationCustomerPortalService _customerPortalService;

    public QuotationLineMutationService(
        IQuotationWorkflowStore store,
        IQuotationCustomerPortalService customerPortalService)
    {
        _store = store;
        _customerPortalService = customerPortalService;
    }

    public async Task<QuotationResponseDto> CancelQuotationDetailAsync(long quotationId, long quotationDetailId, QuotationDetailCancelRequestDto request)
    {
        if (quotationId <= 0 || quotationDetailId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá hoặc mã hạng mục không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự thực hiện hủy hạng mục.");
        }

        var cancelReason = QuotationValidationHelper.NormalizeText(request.CancelReason, 500);
        if (string.IsNullOrWhiteSpace(cancelReason))
        {
            throw new InvalidOperationException("Bắt buộc nhập lý do hủy hạng mục.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.IsAppliedToServiceOrder ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không được hủy hạng mục trên báo giá đã duyệt và đã đồng bộ vào SOD.");
        }

        var detail = quotation.QuotationDetails.FirstOrDefault(x => x.Id == quotationDetailId);
        if (detail == null)
        {
            throw new InvalidOperationException("Không tìm thấy hạng mục cần hủy.");
        }

        if (string.Equals(detail.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
        {
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        QuotationPricingHelper.CalculateQuotationTotals(quotation, excludedDetailId: detail.Id);

        var previousStatus = quotation.Status;
        var hadInsuranceCoverage = QuotationValidationHelper.ResolveInsuranceCoveredAmount(detail) > 0m;
        var now = VietnamTime.Now;
        detail.Status = QuotationWorkflowConstants.DetailStatusCanceled;
        detail.CancelReason = cancelReason;
        detail.CanceledBy = request.ActorStaffId;
        detail.InsuranceCoveredAmount = 0m;
        detail.CustomerCoveredAmount = 0m;
        detail.PayerType = "CUSTOMER";
        detail.UpdatedAt = now;
        detail.UpdatedBy = request.ActorStaffId;

        quotation.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending;
        quotation.IsReadyForSplitBilling = false;
        quotation.SplitBillingReadyAt = null;
        if (hadInsuranceCoverage)
        {
            quotation.Status = QuotationWorkflowConstants.StatusPendingInsurance;
            quotation.InsuranceApprovalEvidenceUrl = null;
        }
        else
        {
            var requiresInsuranceApproval = QuotationValidationHelper.IsInsuranceApprovalRequiredForQuotation(quotation);
            if (!requiresInsuranceApproval)
            {
                quotation.Status = QuotationWorkflowConstants.StatusInsuranceApproved;
            }
            else
            {
                quotation.Status = previousStatus;
            }
        }
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;
        QuotationPricingHelper.RecalculateQuotationTotals(quotation);
        quotation.InsuranceChangeType = await QuotationValidationHelper.ResolveInsuranceChangeTypeAsync(_store, quotation, null);

        var serviceOrder = await _store.GetServiceOrderForUpdateAsync(quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = quotation.Status;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ActorStaffId;
        }

        await _store.SaveChangesInTransactionAsync();
        await _customerPortalService.NotifyCancellationAlertAsync(quotation, detail, request.ActorStaffId, now);

        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> UpdateSplitBillingAsync(long quotationId, QuotationSplitBillingUpdateRequestDto request)
    {
        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự cập nhật tách bill.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Thiếu dòng báo giá cần cập nhật tách bill.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (string.Equals(quotation.Status, QuotationWorkflowConstants.StatusSuperseded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCanceled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingCustomer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể cập nhật tách bill cho báo giá đã khóa.");
        }

        var now = VietnamTime.Now;
        var updatesByDetailId = request.Items
            .Where(x => x != null && x.QuotationDetailId > 0)
            .GroupBy(x => x.QuotationDetailId)
            .ToDictionary(x => x.Key, x => x.First());

        // Validate the requested state before modifying EF-tracked rows.
        var preview = new Quotation
        {
            QuotationDetails = quotation.QuotationDetails.Select(x => new QuotationDetail
            {
                Id = x.Id, Status = x.Status, Quantity = x.Quantity, UnitPriceAtTime = x.UnitPriceAtTime,
                SubTotal = x.SubTotal, TaxRate = x.TaxRate, PayerType = x.PayerType,
                InsuranceCoveredAmount = x.InsuranceCoveredAmount, CustomerCoveredAmount = x.CustomerCoveredAmount
            }).ToList(),
            QuotationDiscounts = (quotation.QuotationDiscounts ?? new List<QuotationDiscount>()).Select(x => new QuotationDiscount
            {
                QuotationDetailId = x.QuotationDetailId, DiscountType = x.DiscountType, ValueType = x.ValueType,
                DiscountValue = x.DiscountValue, AppliedAmount = x.AppliedAmount
            }).ToList()
        };
        ApplyUpdates(preview, true);
        QuotationPricingHelper.CalculateQuotationTotals(preview);
        ApplyUpdates(quotation, false);

        void ApplyUpdates(Quotation target, bool isPreview)
        {
            foreach (var detail in target.QuotationDetails)
            {
                if (string.Equals(detail.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!updatesByDetailId.TryGetValue(detail.Id, out var update))
                {
                    continue;
                }

                if (update.Quantity.HasValue)
                {
                    var normalizedQuantity = update.Quantity.Value <= 0m ? 1m : update.Quantity.Value;
                    detail.Quantity = normalizedQuantity;
                }

                var quantity = detail.Quantity <= 0 ? 1m : detail.Quantity;
                var unitPrice = detail.UnitPriceAtTime < 0 ? 0m : detail.UnitPriceAtTime;
                var subTotal = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
                decimal nextDiscount;

                if (update.DiscountPercentage.HasValue)
                {
                    var normalizedPercent = update.DiscountPercentage.Value < 0m ? 0m : update.DiscountPercentage.Value;
                    normalizedPercent = Math.Min(normalizedPercent, 100m);
                    nextDiscount = Math.Round(subTotal * (normalizedPercent / 100m), 2, MidpointRounding.AwayFromZero);
                }
                else if (update.DiscountValue.HasValue)
                {
                    var normalizedDiscount = update.DiscountValue.Value < 0m ? 0m : update.DiscountValue.Value;
                    nextDiscount = Math.Min(normalizedDiscount, Math.Max(0m, subTotal));
                }
                else
                {
                    var currentDiscountPercentage = QuotationPricingHelper.ResolveQuotationDetailDiscountPercentage(detail, target.QuotationDiscounts);
                    var normalizedPercent = currentDiscountPercentage.HasValue
                        ? Math.Min(Math.Max(currentDiscountPercentage.Value, 0m), 100m)
                        : (decimal?)null;
                    if (normalizedPercent.HasValue)
                    {
                        nextDiscount = Math.Round(subTotal * (normalizedPercent.Value / 100m), 2, MidpointRounding.AwayFromZero);
                    }
                    else
                    {
                        nextDiscount = QuotationPricingHelper.ResolveQuotationDetailDiscountAmount(detail, target.QuotationDiscounts);
                    }
                }

                nextDiscount = Math.Min(Math.Max(0m, nextDiscount), Math.Max(0m, subTotal));
                if (update.DiscountPercentage.HasValue || update.DiscountValue.HasValue)
                {
                    if (isPreview)
                    {
                        var discounts = target.QuotationDiscounts;
                        foreach (var row in discounts.Where(x => x.QuotationDetailId == detail.Id).ToList())
                        {
                            discounts.Remove(row);
                        }
                        if (nextDiscount > 0m)
                        {
                            discounts.Add(new QuotationDiscount { QuotationDetailId = detail.Id, DiscountValue = nextDiscount, AppliedAmount = nextDiscount });
                        }
                    }
                    else
                    {
                        ReplaceQuotationDetailDiscountWithManual(target, detail.Id, nextDiscount, request.ActorStaffId, now, request.Note);
                    }
                }
                var taxRate = detail.TaxRate < 0 ? 0m : detail.TaxRate;
                var taxableBase = Math.Max(0m, subTotal - nextDiscount);
                var taxAmount = Math.Round(taxableBase * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
                var previewFinalAmount = Math.Round(Math.Max(0m, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);
                var split = QuotationPricingHelper.CalculateSplitBilling(
                    previewFinalAmount,
                    update.InsuranceCoveredAmount,
                    null,
                    update.PayerType ?? detail.PayerType);

                detail.PayerType = split.PayerType;
                detail.InsuranceCoveredAmount = split.InsuranceCoveredAmount;
                detail.CustomerCoveredAmount = split.CustomerCoveredAmount;
                detail.UpdatedAt = now;
                detail.UpdatedBy = request.ActorStaffId;
            }

        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var cleaned = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        var requiresInsuranceApproval = QuotationValidationHelper.IsInsuranceApprovalRequiredForQuotation(quotation);
        quotation.Status = requiresInsuranceApproval ? QuotationWorkflowConstants.StatusPendingInsurance : QuotationWorkflowConstants.StatusInsuranceApproved;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;
        QuotationPricingHelper.RecalculateQuotationTotals(quotation);
        QuotationValidationHelper.ValidateQuotationSplitBilling(quotation);
        quotation.InsuranceChangeType = await QuotationValidationHelper.ResolveInsuranceChangeTypeAsync(_store, quotation, null);

        var serviceOrder = await _store.GetServiceOrderForUpdateAsync(quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = quotation.Status;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ActorStaffId;
        }

        await _store.SaveChangesInTransactionAsync();
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    public async Task<IReadOnlyList<QuotationDiscountResponseDto>> UpsertQuotationDiscountsAsync(long quotationId, QuotationDiscountUpsertRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự cập nhật giảm giá báo giá.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (string.Equals(quotation.Status, QuotationWorkflowConstants.StatusSuperseded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCanceled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingCustomer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể cập nhật giảm giá cho báo giá đã khóa.");
        }

        var normalizedItems = await QuotationPricingHelper.NormalizeQuotationDiscountItemsAsync(
            _store,
            request.Items,
            request.ActorStaffId,
            quotationId);

        var existingRows = quotation.QuotationDiscounts?.ToList() ?? new List<QuotationDiscount>();
        var lineDiscounts = existingRows.Where(x => x.QuotationDetailId.HasValue || x.QuotationDetail != null).ToList();
        var quotationDiscounts = existingRows.Except(lineDiscounts).ToList();
        var prospectiveDiscounts = lineDiscounts.Concat(normalizedItems).ToList();

        // Validate without discarding discounts attached to individual lines.
        QuotationPricingHelper.CalculateQuotationTotals(quotation, prospectiveDiscounts);
        if (quotationDiscounts.Count > 0)
        {
            _store.RemoveQuotationDiscounts(quotationDiscounts);
        }

        if (normalizedItems.Count > 0)
        {
            _store.AddQuotationDiscounts(normalizedItems);
        }
        quotation.QuotationDiscounts = prospectiveDiscounts;

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var cleaned = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        var now = VietnamTime.Now;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;
        QuotationPricingHelper.RecalculateQuotationTotals(quotation);
        quotation.InsuranceChangeType = await QuotationValidationHelper.ResolveInsuranceChangeTypeAsync(_store, quotation, null);

        await _store.SaveChangesInTransactionAsync();

        return normalizedItems
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.Id)
            .Select(QuotationMappingHelper.MapQuotationDiscountResponse)
            .ToList();
    }

    public async Task DecideMandatoryOverrideAsync(long requestId, MandatoryOverrideDecisionRequestDto request)
    {
        if (requestId <= 0 || request == null || request.DecidedBy <= 0)
        {
            throw new InvalidOperationException("Invalid mandatory override decision.");
        }

        var row = await _store.GetMandatoryOverrideRequestForUpdateAsync(requestId);
        if (row == null)
        {
            throw new InvalidOperationException("Mandatory override request not found.");
        }

        if (!string.Equals(row.Status, nameof(MandatoryOverrideStatusCode.PENDING_FOREMAN), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        row.Status = request.Approve
            ? nameof(MandatoryOverrideStatusCode.APPROVED)
            : nameof(MandatoryOverrideStatusCode.REJECTED);
        row.DecidedBy = request.DecidedBy;
        row.DecidedAt = VietnamTime.Now;
        row.DecisionNote = QuotationValidationHelper.NormalizeText(request.Note, 1000);
        await _store.SaveChangesInTransactionAsync();
    }

    public async Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự thao tác.");
        }

        var quotation = await _store.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.IsAppliedToServiceOrder || quotation.Status == QuotationWorkflowConstants.StatusInsuranceApproved)
        {
            throw new InvalidOperationException("Không thể thay thế báo giá đã áp vào lệnh sửa chữa.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusSuperseded)
        {
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        }

        var now = VietnamTime.Now;
        quotation.Status = QuotationWorkflowConstants.StatusSuperseded;
        quotation.UpdatedBy = actorStaffId;
        quotation.UpdatedAt = now;
        if (!string.IsNullOrWhiteSpace(note))
        {
            var cleaned = note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        await _store.SaveChangesInTransactionAsync();
        return QuotationMappingHelper.MapQuotationResponse(quotation);
    }

    private void ReplaceQuotationDetailDiscountWithManual(
        Quotation quotation,
        long quotationDetailId,
        decimal discountAmount,
        long actorStaffId,
        DateTime now,
        string? note)
    {
        if (quotation == null || quotationDetailId <= 0)
        {
            return;
        }

        var trackedDiscounts = quotation.QuotationDiscounts ?? new List<QuotationDiscount>();
        var existingLineDiscounts = trackedDiscounts
            .Where(x => x != null && x.QuotationDetailId == quotationDetailId)
            .ToList();

        if (existingLineDiscounts.Count > 0)
        {
            _store.RemoveQuotationDiscounts(existingLineDiscounts);
            foreach (var row in existingLineDiscounts)
            {
                trackedDiscounts.Remove(row);
            }
        }

        var normalizedAmount = discountAmount < 0m ? 0m : discountAmount;
        if (normalizedAmount <= 0m)
        {
            return;
        }

        var manualDiscount = new QuotationDiscount
        {
            QuotationId = quotation.Id,
            QuotationDetailId = quotationDetailId,
            DiscountType = QuotationWorkflowConstants.DiscountTypeManual,
            DiscountValue = normalizedAmount,
            ValueType = QuotationWorkflowConstants.DiscountValueTypeAmount,
            AppliedAmount = normalizedAmount,
            Reason = QuotationValidationHelper.NormalizeText(note, 500) ?? "Line discount updated from split billing",
            AppliedBy = actorStaffId,
            CreatedAt = now
        };

        _store.AddQuotationDiscount(manualDiscount);
        trackedDiscounts.Add(manualDiscount);
        quotation.QuotationDiscounts = trackedDiscounts;
    }
}
