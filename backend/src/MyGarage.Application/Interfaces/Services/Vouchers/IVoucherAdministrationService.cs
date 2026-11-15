using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Application.Vouchers;

public sealed record VoucherAdminPageResult(
    IReadOnlyList<VoucherResponseDto> Items,
    int Total,
    IReadOnlyList<string> Statuses);

public sealed record VoucherReportPageResult(
    IReadOnlyList<VoucherResponseDto> Items,
    int Total,
    long Issued,
    long Redeemed,
    decimal BudgetLimit,
    decimal BudgetUsed);

public sealed record VoucherDashboardResult(
    int ActiveCount,
    long Issued,
    long Redeemed,
    decimal BudgetLimit,
    decimal BudgetUsed,
    decimal TotalDiscount,
    int PendingApprovals,
    IReadOnlyList<VoucherResponseDto> TopVouchers,
    IReadOnlyList<VoucherNearQuotaItem> NearQuotaVouchers,
    IReadOnlyList<VoucherApprovalSummaryItem> ApprovalItems);

public interface IVoucherAdministrationService
{
    Task<VoucherAdminPageResult> GetPageAsync(
        string? search,
        string? status,
        int? page,
        int? pageSize,
        bool includeDeleted = true,
        CancellationToken cancellationToken = default);

    Task<VoucherReportPageResult> GetReportPageAsync(
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default);

    Task<VoucherDashboardResult> GetDashboardAsync(
        CancellationToken cancellationToken = default);
}
