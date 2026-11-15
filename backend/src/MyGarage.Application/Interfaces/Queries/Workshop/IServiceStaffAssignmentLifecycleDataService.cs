using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IServiceStaffAssignmentLifecycleDataService
{
    Task<ServiceStaffAssignment?> GetAssignmentWithOrderAsync(long id, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
