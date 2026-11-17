using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class QuotationMappingHelper
{
    internal sealed class QuotationDetailSupplementaryMeta
    {
        public bool IsSupplementary { get; init; }
        public string ServiceSource { get; init; } = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
        public string? SupplementaryReasonCode { get; init; }
        public long? SourceQuotationId { get; init; }
    }

    internal static QuotationPublicViewResponseDto MapPublicQuotationResponse(
        Quotation quotation,
        IReadOnlyDictionary<long, QuotationDetailSupplementaryMeta>? supplementaryMetaByDetailId = null)
    {
        var status = quotation.Status ?? QuotationWorkflowConstants.StatusDraft;
        var canAct = string.Equals(status, QuotationWorkflowConstants.StatusPendingCustomer, StringComparison.OrdinalIgnoreCase);

        return new QuotationPublicViewResponseDto
        {
            QuotationId = quotation.Id,
            Code = quotation.Code,
            Title = quotation.Title,
            VersionNo = quotation.VersionNo,
            Status = status,
            ValidUntil = quotation.ValidUntil,
            CustomerName = quotation.ServiceOrder?.Customer?.FullName ?? "-",
            CustomerEmail = quotation.ServiceOrder?.Customer?.Email,
            VehicleLicensePlate = quotation.ServiceOrder?.Vehicle?.LicensePlate ?? "-",
            FinalAmount = quotation.FinalAmount,
            TotalInsuranceAmount = quotation.TotalInsuranceAmount,
            TotalCustomerAmount = quotation.TotalCustomerAmount,
            CanApprove = canAct,
            CanReject = canAct,
            Details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
                .OrderBy(x => x.Id)
                .Select(x =>
                {
                    var supplementaryMeta = ResolveQuotationDetailSupplementaryMeta(quotation, x, supplementaryMetaByDetailId);
                    return new QuotationPublicDetailResponseDto
                    {
                        SourceQuotationId = supplementaryMeta.SourceQuotationId,
                        ServiceSource = supplementaryMeta.ServiceSource,
                        IsSupplementary = supplementaryMeta.IsSupplementary,
                        SupplementaryReasonCode = supplementaryMeta.SupplementaryReasonCode,
                        Id = x.Id,
                        ItemType = x.ItemType,
                        ItemName = x.ItemName,
                        Quantity = x.Quantity,
                        UnitPriceAtTime = x.UnitPriceAtTime,
                        SubTotal = x.SubTotal,
                        DiscountValue = QuotationPricingHelper.ResolveQuotationDetailDiscountAmount(x, quotation.QuotationDiscounts),
                        TaxRate = x.TaxRate,
                        TaxAmount = x.TaxAmount,
                        FinalAmount = x.FinalAmount,
                        IsMandatory = x.IsMandatory,
                        Status = string.IsNullOrWhiteSpace(x.Status) ? QuotationWorkflowConstants.DetailStatusActive : x.Status,
                        CancelReason = x.CancelReason
                    };
                })
                .ToList()
        };
    }

    internal static QuotationDetail BuildQuotationDetailEntity(QuotationCreateItemDto dto, long serviceOrderId, long? actorStaffId, DateTime now)
    {
        var quantity = dto.Quantity <= 0 ? 1m : dto.Quantity;
        var unitPrice = dto.UnitPriceAtTime < 0 ? 0 : dto.UnitPriceAtTime;
        var discount = dto.DiscountValue < 0 ? 0 : dto.DiscountValue;
        var taxRate = dto.TaxRate < 0 ? 0 : dto.TaxRate;
        var subTotal = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var taxableBase = Math.Max(0, subTotal - discount);
        var taxAmount = Math.Round(taxableBase * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
        var finalAmount = Math.Round(Math.Max(0, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);
        var normalizedPayerType = string.IsNullOrWhiteSpace(dto.PayerType) ? "CUSTOMER" : dto.PayerType.Trim().ToUpperInvariant();
        var split = QuotationPricingHelper.CalculateSplitBilling(finalAmount, dto.InsuranceCoveredAmount, dto.CustomerCoveredAmount, normalizedPayerType);
        var normalizedDetailStatus = QuotationValidationHelper.NormalizeQuotationDetailStatus(dto.Status);
        var cancelReason = normalizedDetailStatus == QuotationWorkflowConstants.DetailStatusCanceled
            ? QuotationValidationHelper.NormalizeText(dto.CancelReason, 500) ?? "Khách yêu cầu hủy hạng mục"
            : null;
        var canceledBy = normalizedDetailStatus == QuotationWorkflowConstants.DetailStatusCanceled
            ? (dto.CanceledBy ?? actorStaffId)
            : null;

        return new QuotationDetail
        {
            ServiceOrderId = serviceOrderId,
            SourceQuotationDetailId = dto.SourceQuotationDetailId,
            DiagnosisRecommendationId = ResolveDiagnosisRecommendationId(dto),
            ItemType = (dto.ItemType ?? string.Empty).Trim().ToUpperInvariant(),
            ItemId = dto.ItemId,
            ItemName = (dto.ItemName ?? string.Empty).Trim(),
            Description = QuotationValidationHelper.NormalizeText(dto.Description, 1000),
            Quantity = quantity,
            UnitPriceAtTime = unitPrice,
            CostPriceAtTime = dto.CostPriceAtTime,
            SubTotal = subTotal,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            FinalAmount = finalAmount,
            PayerType = split.PayerType,
            InsuranceCompanyId = dto.InsuranceCompanyId,
            InsuranceCoveredAmount = split.InsuranceCoveredAmount,
            CustomerCoveredAmount = split.CustomerCoveredAmount,
            ApprovalStatus = QuotationWorkflowConstants.ApprovalPending,
            Status = normalizedDetailStatus,
            CancelReason = cancelReason,
            CanceledBy = canceledBy,
            IsBlocking = dto.IsBlocking,
            IsMandatory = dto.IsMandatory,
            EstimatedMinutes = dto.EstimatedMinutes,
            IsReturnOldPart = dto.IsReturnOldPart,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };
    }

    private static long? ResolveDiagnosisRecommendationId(QuotationCreateItemDto dto)
    {
        var direct = dto.DiagnosisRecommendationId.GetValueOrDefault();
        if (direct > 0)
        {
            return direct;
        }

        var legacyAlias = dto.DiagnoseRecommendationId.GetValueOrDefault();
        return legacyAlias > 0 ? legacyAlias : null;
    }

    internal static ServiceOrderDetail BuildServiceOrderDetailFromQuotation(
        QuotationDetail detail,
        Quotation quotation,
        long serviceOrderId,
        long actorStaffId,
        DateTime now,
        bool isTechnicianSupplementary)
    {
        var normalizedItemType = NormalizeServiceOrderDetailItemType(detail.ItemType);
        var normalizedPayerType = NormalizeServiceOrderDetailPayerType(
            detail.PayerType,
            detail.InsuranceCoveredAmount,
            detail.CustomerCoveredAmount);
        var isSupplementary = quotation.VersionNo > 1;
        var supplementaryReason = ResolveSupplementaryReasonCode(isSupplementary, isTechnicianSupplementary);
        var serviceSource = ResolveServiceSourceCode(isSupplementary, supplementaryReason);
        var itemId = detail.ItemId;
        var standardServiceId = normalizedItemType == "LABOR" && itemId.HasValue && itemId.Value > 0
            ? itemId
            : null;

        return new ServiceOrderDetail
        {
            ServiceOrderId = serviceOrderId,
            QuotationDetailId = detail.Id,
            StandardServiceId = standardServiceId,
            ItemType = normalizedItemType,
            ItemId = itemId,
            ItemName = detail.ItemName,
            Description = detail.Description,
            Quantity = detail.Quantity <= 0 ? 1m : detail.Quantity,
            EstimatedMinutes = detail.EstimatedMinutes,
            CostPriceAtTime = detail.CostPriceAtTime,
            UnitPriceAtTime = detail.UnitPriceAtTime,
            Unit = "EA",
            SubTotal = detail.SubTotal,
            DiscountAllocatedAmount = QuotationPricingHelper.ResolveQuotationDetailDiscountAmount(detail, quotation.QuotationDiscounts),
            TaxRate = detail.TaxRate,
            TaxAmount = detail.TaxAmount,
            FinalAmount = detail.FinalAmount,
            PayerType = normalizedPayerType,
            InsuranceCompanyId = detail.InsuranceCompanyId,
            InsuranceRequestedAmount = detail.InsuranceCoveredAmount,
            CustomerCoPayAmount = detail.CustomerCoveredAmount,
            Status = nameof(ServiceOrderDetailStatusCode.APPROVED),
            IsReturnOldPart = detail.IsReturnOldPart,
            ServiceSource = serviceSource,
            IsSupplementary = isSupplementary,
            SupplementaryReasonCode = supplementaryReason,
            SourceQuotationId = quotation.Id,
            SupplementaryRequestedByStaffId = isSupplementary ? (quotation.CreatedBy ?? actorStaffId) : null,
            SupplementaryRequestedAt = isSupplementary ? (quotation.CreatedAt ?? now) : null,
            CustomerApprovedAt = isSupplementary ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };
    }

    internal static string NormalizeServiceOrderDetailItemType(string? itemType)
    {
        var normalized = (itemType ?? string.Empty).Trim().ToUpperInvariant();

        return normalized switch
        {
            "PART" or "PARTS" or "PRODUCT" or "MATERIAL" or "INVENTORY" => "PART",
            "SERVICE" or "STANDARD_SERVICE" or "LABOR" => "LABOR",
            _ => "LABOR"
        };
    }

    internal static string NormalizeServiceOrderDetailPayerType(string? payerType, decimal? insuranceAmount, decimal? customerAmount)
    {
        var normalized = (payerType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == "INSURANCE" || normalized == "CUSTOMER")
        {
            return normalized;
        }

        // ServiceOrderDetails currently supports only INSURANCE/CUSTOMER.
        // For split rows, keep accounting split amounts and mark payer by dominant coverage side.
        var insurance = insuranceAmount ?? 0m;
        var customer = customerAmount ?? 0m;
        return insurance > 0m && insurance >= customer ? "INSURANCE" : "CUSTOMER";
    }

    internal static async Task<Dictionary<long, QuotationDetailSupplementaryMeta>> BuildSupplementaryMetaByQuotationDetailIdAsync(
        IQuotationLineageQueryStore store,
        long serviceOrderId)
    {
        var result = new Dictionary<long, QuotationDetailSupplementaryMeta>();
        if (serviceOrderId <= 0)
        {
            return result;
        }

        var sodRows = await store.GetSodSupplementaryRowsAsync(serviceOrderId);

        foreach (var row in sodRows
                     .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                     .ThenByDescending(x => x.Id))
        {
            if (result.ContainsKey(row.QuotationDetailId))
            {
                continue;
            }

            var normalizedSource = NormalizeServiceSourceForResponse(row.ServiceSource);
            var normalizedReason = NormalizeSupplementaryReasonForResponse(row.SupplementaryReasonCode);
            var isSupplementary = row.IsSupplementary ||
                                  string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN), StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER), StringComparison.OrdinalIgnoreCase);

            if (!isSupplementary)
            {
                normalizedSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
                normalizedReason = null;
            }

            result[row.QuotationDetailId] = new QuotationDetailSupplementaryMeta
            {
                IsSupplementary = isSupplementary,
                ServiceSource = normalizedSource,
                SupplementaryReasonCode = normalizedReason,
                SourceQuotationId = isSupplementary ? row.SourceQuotationId : null
            };
        }

        var quotationRows = await store.GetQuotationLineageRowsAsync(serviceOrderId);
        var versionByQuotationId = quotationRows.ToDictionary(x => x.Id, x => x.VersionNo);
        var diagnosisAssignmentIdByQuotationId = quotationRows.ToDictionary(x => x.Id, x => x.DiagnosisAssignmentId);
        var initialDiagnosisAssignmentId = quotationRows
            .OrderBy(x => x.VersionNo)
            .ThenBy(x => x.Id)
            .Select(x => x.DiagnosisAssignmentId)
            .FirstOrDefault();

        var quotationDetailRows = await store.GetQuotationDetailLineageRowsAsync(serviceOrderId);
        var quotationDetailById = quotationDetailRows.ToDictionary(x => x.Id, x => x);

        QuotationDetailSupplementaryMeta ResolveFromLineage(long quotationDetailId)
        {
            var visited = new HashSet<long>();
            var cursorId = quotationDetailId;
            long? rootQuotationId = null;
            var rootVersionNo = 1;

            while (cursorId > 0 && visited.Add(cursorId))
            {
                if (result.TryGetValue(cursorId, out var existingMeta))
                {
                    return existingMeta;
                }

                if (!quotationDetailById.TryGetValue(cursorId, out var detailNode))
                {
                    break;
                }

                rootQuotationId = detailNode.QuotationId;
                if (versionByQuotationId.TryGetValue(detailNode.QuotationId, out var mappedVersionNo))
                {
                    rootVersionNo = mappedVersionNo;
                }

                if (!detailNode.SourceQuotationDetailId.HasValue || detailNode.SourceQuotationDetailId.Value <= 0)
                {
                    break;
                }

                cursorId = detailNode.SourceQuotationDetailId.Value;
            }

            var isSupplementary = rootVersionNo > 1;
            var isTechnicianSupplementary = isSupplementary &&
                rootQuotationId.HasValue &&
                diagnosisAssignmentIdByQuotationId.TryGetValue(rootQuotationId.Value, out var rootDiagnosisAssignmentId) &&
                rootDiagnosisAssignmentId.HasValue &&
                initialDiagnosisAssignmentId.HasValue &&
                rootDiagnosisAssignmentId.Value != initialDiagnosisAssignmentId.Value;
            var supplementaryReasonCode = ResolveSupplementaryReasonCode(isSupplementary, isTechnicianSupplementary);
            var serviceSource = ResolveServiceSourceCode(isSupplementary, supplementaryReasonCode);

            return new QuotationDetailSupplementaryMeta
            {
                IsSupplementary = isSupplementary,
                ServiceSource = serviceSource,
                SupplementaryReasonCode = supplementaryReasonCode,
                SourceQuotationId = isSupplementary ? rootQuotationId : null
            };
        }

        foreach (var row in quotationDetailRows)
        {
            if (result.ContainsKey(row.Id))
            {
                continue;
            }

            result[row.Id] = ResolveFromLineage(row.Id);
        }

        return result;
    }

    private static string NormalizeServiceSourceForResponse(string? source)
    {
        var normalized = (source ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN),
            nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER),
            _ => nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
        };
    }

    private static string? ResolveSupplementaryReasonCode(bool isSupplementary, bool isTechnicianSupplementary)
    {
        if (!isSupplementary)
        {
            return null;
        }

        return isTechnicianSupplementary
            ? nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING)
            : nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST);
    }

    private static string ResolveServiceSourceCode(bool isSupplementary, string? supplementaryReasonCode)
    {
        if (!isSupplementary)
        {
            return nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
        }

        return string.Equals(
            supplementaryReasonCode,
            nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
            StringComparison.OrdinalIgnoreCase)
            ? nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN)
            : nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER);
    }

    private static string? NormalizeSupplementaryReasonForResponse(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING) => nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
            nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST) => nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST),
            _ => null
        };
    }

    private static QuotationDetailSupplementaryMeta ResolveQuotationDetailSupplementaryMeta(
        Quotation quotation,
        QuotationDetail detail,
        IReadOnlyDictionary<long, QuotationDetailSupplementaryMeta>? supplementaryMetaByDetailId)
    {
        if (supplementaryMetaByDetailId != null &&
            supplementaryMetaByDetailId.TryGetValue(detail.Id, out var metaFromMap))
        {
            return metaFromMap;
        }

        var isSupplementary = quotation.VersionNo > 1;
        var supplementaryReasonCode = ResolveSupplementaryReasonCode(isSupplementary, false);
        var serviceSource = ResolveServiceSourceCode(isSupplementary, supplementaryReasonCode);

        return new QuotationDetailSupplementaryMeta
        {
            IsSupplementary = isSupplementary,
            ServiceSource = serviceSource,
            SupplementaryReasonCode = supplementaryReasonCode,
            SourceQuotationId = isSupplementary ? quotation.Id : null
        };
    }

    internal static QuotationResponseDto MapQuotationResponse(
        Quotation quotation,
        IReadOnlyCollection<string>? warnings = null,
        IReadOnlyDictionary<long, QuotationDetailSupplementaryMeta>? supplementaryMetaByDetailId = null)
    {
        return new QuotationResponseDto
        {
            Id = quotation.Id,
            Code = quotation.Code,
            ServiceOrderId = quotation.ServiceOrderId,
            DiagnosisId = quotation.DiagnosisId,
            DiagnosisAssignmentId = quotation.DiagnosisAssignmentId,
            Title = quotation.Title,
            QuotationType = quotation.QuotationType,
            VersionNo = quotation.VersionNo,
            Status = quotation.Status,
            InsuranceClaimNumber = quotation.InsuranceClaimNumber,
            InsuranceApprovalEvidenceUrl = quotation.InsuranceApprovalEvidenceUrl,
            TechnicalApprovedBy = quotation.TechnicalApprovedBy,
            TechnicalApprovedAt = quotation.TechnicalApprovedAt,
            TechnicalApprovalStatus = quotation.TechnicalApprovalStatus,
            RequiresFinancialApproval = quotation.RequiresFinancialApproval,
            FinancialApprovedBy = quotation.FinancialApprovedBy,
            FinancialApprovedAt = quotation.FinancialApprovedAt,
            FinancialApprovalStatus = quotation.FinancialApprovalStatus,
            CustomerDecisionBy = quotation.CustomerDecisionBy,
            CustomerDecisionAt = quotation.CustomerDecisionAt,
            CustomerDecisionStatus = quotation.CustomerDecisionStatus,
            ValidUntil = quotation.ValidUntil,
            SubTotalAmount = quotation.SubTotalAmount,
            TotalDiscount = QuotationPricingHelper.ResolveQuotationTotalDiscountAmount(quotation),
            TaxAmount = quotation.TaxAmount,
            FinalAmount = quotation.FinalAmount,
            TotalInsuranceAmount = quotation.TotalInsuranceAmount,
            TotalCustomerAmount = quotation.TotalCustomerAmount,
            InsuranceChangeType = QuotationValidationHelper.NormalizeInsuranceChangeType(quotation.InsuranceChangeType),
            Warnings = warnings?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? new List<string>(),
            IsAppliedToServiceOrder = quotation.IsAppliedToServiceOrder,
            IsReadyForSplitBilling = quotation.IsReadyForSplitBilling,
            SplitBillingReadyAt = quotation.SplitBillingReadyAt,
            AppliedAt = quotation.AppliedAt,
            Discounts = (quotation.QuotationDiscounts ?? new List<QuotationDiscount>())
                .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Select(MapQuotationDiscountResponse)
                .ToList(),
            Details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
                .OrderBy(x => x.Id)
                .Select(x =>
                {
                    var supplementaryMeta = ResolveQuotationDetailSupplementaryMeta(quotation, x, supplementaryMetaByDetailId);
                    return new QuotationDetailResponseDto
                    {
                        // Enforce a stable business contract so frontend can split initial/supplementary
                        // without inferring from versioning rules.
                        SourceQuotationId = supplementaryMeta.SourceQuotationId,
                        ServiceSource = supplementaryMeta.ServiceSource,
                        IsSupplementary = supplementaryMeta.IsSupplementary,
                        SupplementaryReasonCode = supplementaryMeta.SupplementaryReasonCode,
                        Id = x.Id,
                        SourceQuotationDetailId = x.SourceQuotationDetailId,
                        ItemType = x.ItemType,
                        ItemId = x.ItemId,
                        ItemName = x.ItemName,
                        Description = x.Description,
                        Quantity = x.Quantity,
                        UnitPriceAtTime = x.UnitPriceAtTime,
                        CostPriceAtTime = x.CostPriceAtTime,
                        SubTotal = x.SubTotal,
                        DiscountPercentage = QuotationPricingHelper.ResolveQuotationDetailDiscountPercentage(x, quotation.QuotationDiscounts),
                        DiscountValue = QuotationPricingHelper.ResolveQuotationDetailDiscountAmount(x, quotation.QuotationDiscounts),
                        TaxRate = x.TaxRate,
                        TaxAmount = x.TaxAmount,
                        FinalAmount = x.FinalAmount,
                        PayerType = x.PayerType,
                        InsuranceCompanyId = x.InsuranceCompanyId,
                        InsuranceCoveredAmount = x.InsuranceCoveredAmount,
                        CustomerCoveredAmount = x.CustomerCoveredAmount,
                        ApprovalStatus = x.ApprovalStatus,
                        RejectReason = x.RejectReason,
                        Status = string.IsNullOrWhiteSpace(x.Status) ? QuotationWorkflowConstants.DetailStatusActive : x.Status,
                        CancelReason = x.CancelReason,
                        CanceledBy = x.CanceledBy,
                        IsBlocking = x.IsBlocking,
                        IsMandatory = x.IsMandatory,
                        EstimatedMinutes = x.EstimatedMinutes,
                        IsReturnOldPart = x.IsReturnOldPart
                    };
                })
                .ToList()
        };
    }

    internal static QuotationDiscountResponseDto MapQuotationDiscountResponse(QuotationDiscount discount)
    {
        return new QuotationDiscountResponseDto
        {
            Id = discount.Id,
            QuotationId = discount.QuotationId,
            DiscountType = discount.DiscountType,
            CustomerTierId = discount.CustomerTierId,
            VoucherId = discount.VoucherId,
            DiscountValue = discount.DiscountValue,
            ValueType = discount.ValueType,
            AppliedAmount = discount.AppliedAmount,
            Reason = discount.Reason,
            AppliedBy = discount.AppliedBy,
            DiscountCategory = discount.DiscountCategory,
            CreatedAt = discount.CreatedAt
        };
    }
}
