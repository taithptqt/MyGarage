using MyGarage.Models;

namespace MyGarage.Application.Bays;

public interface IBayAllocationStore
{
    Task<BayAllocation?> GetActiveAllocationForBayAsync(long bayId, CancellationToken ct = default);
    Task<BayAllocation?> GetActiveAllocationForBayAndVehicleAsync(long bayId, long vehicleId, CancellationToken ct = default);
    Task<IReadOnlyList<BayAllocation>> GetTimelineAllocationsAsync(long branchId, DateTime startOfDay, DateTime endOfDay, CancellationToken ct = default);
    Task<BayAllocation> GetAllocationDetailsAsync(long allocationId, bool includeReleasedByStaff, CancellationToken ct = default);
    Task AllocateBayInTransactionAsync(Bay bay, BayAllocation allocation, BayStatusLog statusLog, long vehicleId, DateTime now, CancellationToken ct = default);
    Task ReleaseBayInTransactionAsync(Bay bay, BayStatusLog statusLog, DateTime now, CancellationToken ct = default);
    Task TransferBayInTransactionAsync(Bay sourceBay, Bay targetBay, BayAllocation targetAllocation, BayStatusLog sourceLog, BayStatusLog targetLog, long vehicleId, DateTime now, CancellationToken ct = default);
}
