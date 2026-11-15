using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class QuotationValidationHelper
{
    internal static string BuildQuotationCode(long serviceOrderId, int versionNo, DateTime now)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"QT-{now:yyyyMMddHHmmss}-{serviceOrderId}-{versionNo:D3}");
    }

    internal static long? ResolveServiceAdvisorForQuotation(
        long? requestedServiceAdvisorId,
        long? serviceOrderServiceAdvisorId,
        long? serviceOrderReceptionistId)
    {
        if (requestedServiceAdvisorId.HasValue && requestedServiceAdvisorId.Value > 0)
        {
            return requestedServiceAdvisorId.Value;
        }

        if (serviceOrderServiceAdvisorId.HasValue && serviceOrderServiceAdvisorId.Value > 0)
        {
            return serviceOrderServiceAdvisorId.Value;
        }

        if (serviceOrderReceptionistId.HasValue && serviceOrderReceptionistId.Value > 0)
        {
            return serviceOrderReceptionistId.Value;
        }

        return null;
    }

    internal static string BuildQuotationTitle(int versionNo)
    {
        var safeVersion = versionNo > 0 ? versionNo : 1;
        return NormalizeText($"Báo giá V{safeVersion}", 255)!;
    }

    internal static bool EnsureQuotationTitleByVersion(Quotation quotation)
    {
        var expectedTitle = BuildQuotationTitle(quotation.VersionNo);
        if (string.Equals((quotation.Title ?? string.Empty).Trim(), expectedTitle, StringComparison.Ordinal))
        {
            return false;
        }

        quotation.Title = expectedTitle;
        return true;
    }

    internal static string NormalizeQuotationType(string? rawValue)
    {
        var normalized = (rawValue ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == nameof(QuotationTypeCode.INSURANCE) || normalized == nameof(QuotationTypeCode.MIXED))
        {
            return normalized;
        }

        return nameof(QuotationTypeCode.NORMAL);
    }

    internal static string NormalizeQuotationDetailStatus(string? rawValue)
    {
        var normalized = (rawValue ?? string.Empty).Trim().ToUpperInvariant();
        return normalized == QuotationWorkflowConstants.DetailStatusCanceled
            ? QuotationWorkflowConstants.DetailStatusCanceled
            : QuotationWorkflowConstants.DetailStatusActive;
    }

    internal static string? NormalizeText(string? value, int maxLen)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }

    internal static string? NormalizeIdempotencyKey(string? key)
    {
        var normalized = (key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > 120)
        {
            normalized = normalized[..120];
        }

        return normalized;
    }

    private static bool IsCanceledState(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();
        return normalized == QuotationWorkflowConstants.StatusCanceled
            || normalized == QuotationWorkflowConstants.LegacyStatusCancelled;
    }

    internal static void EnsureCustomerDecisionAllowed(Quotation quotation, ServiceOrder serviceOrder)
    {
        if (IsCanceledState(quotation.Status))
        {
            throw new InvalidOperationException("Báo giá đã hủy, không thể duyệt từ liên kết.");
        }

        if (IsCanceledState(serviceOrder.Status))
        {
            throw new InvalidOperationException("Lệnh sửa chữa đã hủy, không thể duyệt báo giá từ liên kết.");
        }
    }

    internal static void ValidateQuotationSplitBilling(Quotation quotation)
    {
        if (quotation?.QuotationDetails == null) return;

        foreach (var detail in quotation.QuotationDetails)
        {
            if (string.Equals(detail.Status, "CANCELED", StringComparison.OrdinalIgnoreCase)) continue;

            var insurance = detail.InsuranceCoveredAmount ?? 0m;
            var customer = detail.CustomerCoveredAmount ?? 0m;
            var total = detail.FinalAmount;

            var diff = Math.Abs(total - (insurance + customer));
            if (diff > 0.05m) // Cho phép sai lệch nhỏ do làm tròn (50 VNĐ)
            {
                throw new InvalidOperationException(
                    $"Dòng hạng mục '{detail.ItemName}' có tổng tiền thanh toán không khớp: " +
                    $"Tổng ({total:N0}) != Bảo hiểm ({insurance:N0}) + Khách ({customer:N0}).");
            }
        }
    }

    internal static bool IsInsuranceApprovalRequiredForQuotation(Quotation quotation)
    {
        if (quotation?.QuotationDetails == null || quotation.QuotationDetails.Count == 0)
        {
            return false;
        }

        return quotation.QuotationDetails.Any(IsInsuranceResponsibleDetail);
    }

    private static bool IsInsuranceResponsibleDetail(QuotationDetail detail)
    {
        if (detail == null || string.Equals(detail.Status, QuotationWorkflowConstants.DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ResolveInsuranceCoveredAmount(detail) > 0m;
    }

    internal static decimal ResolveInsuranceCoveredAmount(QuotationDetail detail)
    {
        if (detail == null)
        {
            return 0m;
        }

        if (detail.InsuranceCoveredAmount.HasValue)
        {
            return Math.Max(0m, detail.InsuranceCoveredAmount.Value);
        }

        var payerType = (detail.PayerType ?? string.Empty).Trim().ToUpperInvariant();
        return payerType is "INSURANCE" or "SPLIT"
            ? Math.Max(0m, detail.FinalAmount)
            : 0m;
    }

    internal static async Task<string> ResolveInsuranceChangeTypeAsync(
        IQuotationLineageQueryStore store,
        Quotation quotation,
        decimal? previousInsuranceAmountOverride)
    {
        if (quotation == null)
        {
            return QuotationWorkflowConstants.InsuranceChangeTypeNone;
        }

        var currentInsuranceAmount = Math.Max(0m, quotation.TotalInsuranceAmount);
        decimal? previousInsuranceAmount = previousInsuranceAmountOverride;
        if (!previousInsuranceAmount.HasValue)
        {
            var previousVersionNo = quotation.VersionNo - 1;
            if (previousVersionNo > 0)
            {
                previousInsuranceAmount = await store.GetPreviousVersionInsuranceAmountAsync(
                    quotation.ServiceOrderId,
                    previousVersionNo);
            }
        }

        if (!previousInsuranceAmount.HasValue)
        {
            return currentInsuranceAmount > 0m
                ? QuotationWorkflowConstants.InsuranceChangeTypeIncrease
                : QuotationWorkflowConstants.InsuranceChangeTypeNone;
        }

        var previous = Math.Max(0m, previousInsuranceAmount.Value);
        if (currentInsuranceAmount > previous)
        {
            return QuotationWorkflowConstants.InsuranceChangeTypeIncrease;
        }

        if (currentInsuranceAmount < previous)
        {
            return QuotationWorkflowConstants.InsuranceChangeTypeDecrease;
        }

        return QuotationWorkflowConstants.InsuranceChangeTypeNone;
    }

    internal static string NormalizeInsuranceChangeType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            QuotationWorkflowConstants.InsuranceChangeTypeIncrease => QuotationWorkflowConstants.InsuranceChangeTypeIncrease,
            QuotationWorkflowConstants.InsuranceChangeTypeDecrease => QuotationWorkflowConstants.InsuranceChangeTypeDecrease,
            _ => QuotationWorkflowConstants.InsuranceChangeTypeNone
        };
    }

    internal static async Task<bool> IsTechnicianSupplementaryQuotationAsync(
        IQuotationLineageQueryStore store,
        Quotation quotation,
        long serviceOrderId)
    {
        if (quotation.VersionNo <= 1)
        {
            return false;
        }

        var initialQuotation = await store.GetInitialQuotationDiagnosisMetaAsync(serviceOrderId);

        if (initialQuotation == null ||
            initialQuotation.Id == quotation.Id ||
            !initialQuotation.DiagnosisAssignmentId.HasValue ||
            !quotation.DiagnosisAssignmentId.HasValue)
        {
            return false;
        }

        return initialQuotation.DiagnosisAssignmentId.Value != quotation.DiagnosisAssignmentId.Value;
    }

    internal static bool IsMandatoryByUrgency(string? urgency)
    {
        var normalized = (urgency ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "CRITICAL" or "SAFETY" or "SAFETY_CRITICAL" or "MANDATORY";
    }
}
