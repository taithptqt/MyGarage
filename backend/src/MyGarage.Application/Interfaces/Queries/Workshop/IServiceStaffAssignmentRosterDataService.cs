using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IServiceStaffAssignmentRosterDataService
{
    Task<ServiceOrderDetailSummary?> GetDetailInfoAsync(long serviceOrderDetailId, CancellationToken ct = default);

    Task<IReadOnlyList<long>> GetExistingAssignedTechnicianIdsAsync(
        long serviceOrderDetailId,
        IReadOnlyCollection<long> technicianIds,
        CancellationToken ct = default);

    Task<Dictionary<long, StaffTechnicianSummary>> GetTechniciansByIdsAsync(
        IReadOnlyCollection<long> technicianIds,
        CancellationToken ct = default);

    Task AddAssignmentsAsync(IEnumerable<ServiceStaffAssignment> rows, CancellationToken ct = default);
}
