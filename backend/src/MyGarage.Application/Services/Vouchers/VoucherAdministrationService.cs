using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;

namespace MyGarage.Application.Vouchers;

public sealed class VoucherAdministrationService(
    IVoucherStore store,
    IMapper mapper) : IVoucherAdministrationService
{
    private readonly IVoucherStore _store = store;
    private readonly IMapper _mapper = mapper;

    public async Task<VoucherAdminPageResult> GetPageAsync(
        string? search,
        string? status,
        int? page,
        int? pageSize,
        bool includeDeleted = true,
        CancellationToken cancellationToken = default)
    {
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            normalizedStatus = status.Trim().ToUpperInvariant();
        }

        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var (data, total, statuses) = await _store.GetLegacyAdminListPageAsync(
            includeDeleted,
            search,
            normalizedStatus,
            currentPage,
            size,
            cancellationToken);

        return new VoucherAdminPageResult(
            _mapper.Map<List<VoucherResponseDto>>(data),
            total,
            statuses);
    }

    public async Task<VoucherReportPageResult> GetReportPageAsync(
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var report = await _store.GetReportPageDataAsync(current, size, cancellationToken);

        return new VoucherReportPageResult(
            _mapper.Map<List<VoucherResponseDto>>(report.Items),
            report.Total,
            report.Issued,
            report.Redeemed,
            report.BudgetLimit,
            report.BudgetUsed);
    }

    public async Task<VoucherDashboardResult> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var dashboard = await _store.GetDashboardDataAsync(cancellationToken);

        return new VoucherDashboardResult(
            dashboard.ActiveCount,
            dashboard.Issued,
            dashboard.Redeemed,
            dashboard.BudgetLimit,
            dashboard.BudgetUsed,
            dashboard.TotalDiscount,
            dashboard.PendingApprovals,
            _mapper.Map<List<VoucherResponseDto>>(dashboard.TopVouchers),
            dashboard.NearQuotaVouchers,
            dashboard.ApprovalItems);
    }
}
