using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Vouchers;

public interface IVoucherCatalogStore
{
    Task<(IReadOnlyList<Voucher> Items, int TotalCount, IReadOnlyList<string> AvailableStatuses)> GetAdminListPageAsync(
        bool includeDeleted,
        string? search,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Voucher> Items, int TotalCount, IReadOnlyList<string> AvailableStatuses)> GetLegacyAdminListPageAsync(
        bool includeDeleted,
        string? search,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<VoucherReportPageData> GetReportPageDataAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<VoucherDashboardData> GetDashboardDataAsync(CancellationToken ct = default);

    Task<Voucher?> GetByIdAsync(long id, bool track = false, CancellationToken ct = default);

    Task<Voucher?> GetByCodeAsync(string normalizedCode, CancellationToken ct = default);

    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken ct = default);

    Task AddVoucherAsync(Voucher entity, CancellationToken ct = default);

    Task SaveVoucherAsync(Voucher entity, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<VoucherApprovalRequest?> GetApprovalRequestByIdAsync(long id, bool track = false, CancellationToken ct = default);

    Task SaveApprovalRequestChangesAsync(VoucherApprovalRequest request, CancellationToken ct = default);
}
