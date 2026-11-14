using System;
using System.Globalization;

namespace MyGarage.Domain.Rules;

public static class PayrollDomainRules
{
    public const string PeriodStatusOpen = "OPEN";
    public const string PeriodStatusCalculated = "CALCULATED";
    public const string PeriodStatusApproved = "APPROVED";
    public const string PeriodStatusPaid = "PAID";
    public const string PeriodStatusLocked = "LOCKED";

    public const string RunStatusDraft = "DRAFT";
    public const string RunStatusCalculated = "CALCULATED";
    public const string RunStatusApproved = "APPROVED";
    public const string RunStatusPaid = "PAID";
    public const string RunStatusLocked = "LOCKED";
    public const string RunStatusCanceled = "CANCELED";

    public const string ItemStatusDraft = "DRAFT";
    public const string ItemStatusApproved = "APPROVED";
    public const string ItemStatusPaid = "PAID";
    public const string ItemStatusAdjusted = "ADJUSTED";

    public static bool TryParsePeriod(string? period, out string normalizedCode, out DateOnly startDate, out DateOnly endDate)
    {
        normalizedCode = string.Empty;
        startDate = default;
        endDate = default;

        if (string.IsNullOrWhiteSpace(period))
        {
            var now = DateTime.UtcNow;
            normalizedCode = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            startDate = new DateOnly(now.Year, now.Month, 1);
            endDate = startDate.AddMonths(1).AddDays(-1);
            return true;
        }

        var trimmed = period.Trim();
        if (!DateOnly.TryParseExact(
                $"{trimmed}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var firstDay))
        {
            return false;
        }

        normalizedCode = firstDay.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        startDate = firstDay;
        endDate = firstDay.AddMonths(1).AddDays(-1);
        return true;
    }

    public static bool ValidatePeriodFormat(string? period, out string normalizedCode, out DateOnly startDate, out DateOnly endDate)
    {
        normalizedCode = string.Empty;
        startDate = default;
        endDate = default;

        if (string.IsNullOrWhiteSpace(period))
        {
            return false;
        }

        return TryParsePeriod(period, out normalizedCode, out startDate, out endDate);
    }

    public static bool CanModifyPayrollRun(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var norm = status.Trim().ToUpperInvariant();
        return norm is RunStatusDraft or RunStatusCalculated;
    }

    public static bool CanAddRunToPeriod(string? periodStatus)
    {
        if (string.IsNullOrWhiteSpace(periodStatus)) return true;
        var norm = periodStatus.Trim().ToUpperInvariant();
        return norm is PeriodStatusOpen or PeriodStatusCalculated;
    }

    public static bool ValidateRunStatusTransition(string? currentStatus, string targetStatus, out string? errorMessage)
    {
        errorMessage = null;
        var curr = (currentStatus ?? string.Empty).Trim().ToUpperInvariant();
        var tgt = (targetStatus ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(curr))
        {
            if (tgt is RunStatusDraft or RunStatusCalculated) return true;
            errorMessage = $"Trạng thái khởi tạo không hợp lệ: {targetStatus}.";
            return false;
        }

        if (curr is RunStatusLocked or RunStatusCanceled)
        {
            errorMessage = $"Không thể chuyển trạng thái từ trạng thái cuối cùng {curr}.";
            return false;
        }

        switch (curr)
        {
            case RunStatusDraft:
                if (tgt is RunStatusDraft or RunStatusCalculated or RunStatusApproved or RunStatusCanceled) return true;
                if (tgt is RunStatusPaid)
                {
                    errorMessage = "Không được phép chuyển trực tiếp từ DRAFT sang PAID mà không qua phê duyệt (APPROVED).";
                    return false;
                }
                break;

            case RunStatusCalculated:
                if (tgt is RunStatusCalculated or RunStatusApproved or RunStatusCanceled) return true;
                if (tgt is RunStatusPaid)
                {
                    errorMessage = "Không được phép chuyển trực tiếp từ CALCULATED sang PAID mà không qua phê duyệt (APPROVED).";
                    return false;
                }
                break;

            case RunStatusApproved:
                if (tgt is RunStatusApproved or RunStatusPaid or RunStatusLocked) return true;
                break;

            case RunStatusPaid:
                if (tgt is RunStatusPaid or RunStatusLocked) return true;
                break;
        }

        errorMessage = $"Chuyển trạng thái từ {curr} sang {tgt} không hợp lệ.";
        return false;
    }

    public static bool CanApprovePayrollRun(string? status, int itemCount, out string? errorMessage)
    {
        errorMessage = null;
        var curr = (status ?? string.Empty).Trim().ToUpperInvariant();

        if (curr is not RunStatusDraft and not RunStatusCalculated)
        {
            errorMessage = $"Chỉ có thể duyệt bảng lương ở trạng thái DRAFT hoặc CALCULATED (hiện tại: {status}).";
            return false;
        }

        if (itemCount <= 0)
        {
            errorMessage = "Bảng lương không có dòng chi tiết nhân viên.";
            return false;
        }

        return true;
    }

    public static bool CanMarkPaidPayrollRun(string? status, out string? errorMessage)
    {
        errorMessage = null;
        var curr = (status ?? string.Empty).Trim().ToUpperInvariant();

        if (curr != RunStatusApproved)
        {
            errorMessage = $"Chỉ có thể chi trả bảng lương đã được duyệt (APPROVED). Trạng thái hiện tại: {status}.";
            return false;
        }

        return true;
    }

    public static bool CanLockPayrollRun(string? status, out string? errorMessage)
    {
        errorMessage = null;
        var curr = (status ?? string.Empty).Trim().ToUpperInvariant();

        if (curr is not RunStatusPaid and not RunStatusApproved)
        {
            errorMessage = $"Chỉ có thể khóa bảng lương ở trạng thái PAID hoặc APPROVED (hiện tại: {status}).";
            return false;
        }

        return true;
    }

    public static int CalculateNextRunNo(int maxExistingRunNo)
    {
        return Math.Max(0, maxExistingRunNo) + 1;
    }

    public static (bool IsReconciled, decimal Discrepancy) ReconcileCommissions(
        decimal itemCommissionTotal,
        decimal approvedTransactionsTotal,
        decimal tolerance = 0.01m)
    {
        var diff = Math.Abs(itemCommissionTotal - approvedTransactionsTotal);
        return (diff <= tolerance, diff);
    }
}
