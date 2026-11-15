using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IServiceStaffAssignmentQueryDataService
{
    Task<(IReadOnlyList<ServiceStaffAssignment> Items, int Total)> GetPagedAsync(
        long? scopedBranchId,
        long? serviceOrderId,
        long? staffId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<bool> AssignmentBelongsToBranchAsync(long assignmentId, long branchId, CancellationToken ct = default);

    Task<bool> ServiceOrderBelongsToBranchAsync(long serviceOrderId, long branchId, CancellationToken ct = default);

    Task<long> GetServiceOrderIdFromDetailIdAsync(long serviceOrderDetailId, CancellationToken ct = default);

    Task<string?> GetDetailPayerTypeAsync(long serviceOrderDetailId, CancellationToken ct = default);
}
