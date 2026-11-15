using System.Collections.Generic;
using MyGarage.Models;

namespace MyGarage.Application.Vouchers;

public interface IVoucherStore :
    IVoucherCatalogStore,
    IVoucherCampaignStore,
    IVoucherRedemptionStore,
    IVoucherValidationStore
{
}

public sealed record VoucherReportPageData(
    IReadOnlyList<Voucher> Items,
    int Total,
    long Issued,
    long Redeemed,
    decimal BudgetLimit,
    decimal BudgetUsed);

public sealed record VoucherApprovalSummaryItem(long Id, string? RequestType, string? RiskLevel);

public sealed record VoucherNearQuotaItem(long Id, string? Code, string? VoucherName, decimal Ratio);

public sealed record VoucherDashboardData(
    int ActiveCount,
    long Issued,
    long Redeemed,
    decimal BudgetLimit,
    decimal BudgetUsed,
    decimal TotalDiscount,
    int PendingApprovals,
    IReadOnlyList<Voucher> TopVouchers,
    IReadOnlyList<VoucherNearQuotaItem> NearQuotaVouchers,
    IReadOnlyList<VoucherApprovalSummaryItem> ApprovalItems);
