using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IVoucherApprovalRequestQueryService
{
    Task<(IReadOnlyList<VoucherApprovalRequest> Items, int Total)> GetPagedAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<VoucherApprovalRequest> Items, int Total)> GetAdminPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
