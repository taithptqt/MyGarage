using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record BranchAdminPageData(
    IReadOnlyList<Branch> Rows,
    int Total,
    int TotalBranches,
    int ActiveBranches);

public interface IBranchAdminQueryService
{
    Task<BranchAdminPageData> GetAdminPageAsync(string? search, int currentPage, int pageSize, CancellationToken ct = default);
}
