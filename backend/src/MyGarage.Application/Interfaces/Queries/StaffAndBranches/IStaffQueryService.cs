using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record StaffAdminListData(
    IReadOnlyList<Staff> Items,
    int Total,
    int TotalStaff,
    int ActiveStaff,
    int RolesUsed);

public interface IStaffQueryService
{
    Task<(IReadOnlyList<Staff> Items, int Total)> GetPagedAsync(
        long? branchId,
        long? teamId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<StaffAdminListData> GetAdminListAsync(
        long? branchId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
