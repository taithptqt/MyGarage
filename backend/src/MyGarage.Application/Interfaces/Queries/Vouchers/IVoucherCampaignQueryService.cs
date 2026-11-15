using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record VoucherCampaignAdminRow(
    VoucherCampaign Campaign,
    int VoucherCount,
    int ActiveVouchers,
    long Redeemed);

public sealed record VoucherCampaignAdminPageResult(
    IReadOnlyList<VoucherCampaignAdminRow> Rows,
    int Total,
    IReadOnlyList<string> Statuses,
    int TotalCampaigns,
    int ActiveCampaigns,
    long TotalRedeemed,
    int BudgetWarningCount,
    IReadOnlyList<VoucherCampaign> WarningRows);

public sealed record VoucherCampaignSummaryResult(VoucherCampaign Campaign, int VoucherCount);

public sealed record VoucherCampaignVoucherPageResult(
    IReadOnlyList<VoucherCampaignVoucherSummaryDto> Items,
    int Total);

public interface IVoucherCampaignQueryService
{
    Task<(IReadOnlyList<VoucherCampaign> Items, int Total)> GetPageAsync(
        string? keyword,
        string? status,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<VoucherCampaignAdminPageResult> GetAdminPageAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<VoucherCampaignSummaryResult?> GetSummaryAsync(long id, CancellationToken ct = default);

    Task<VoucherCampaignVoucherPageResult?> GetVoucherPageAsync(
        long id,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
