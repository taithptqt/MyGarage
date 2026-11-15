using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IBayManagementStore
{
    Task<Bay?> GetBayByIdAsync(long bayId);
    Task<bool> ServiceOrderExistsAsync(long serviceOrderId);
    Task<Vehicle?> GetVehicleByIdAsync(long vehicleId);
    Task AddBayAllocationAsync(BayAllocation allocation);
    Task AddBayStatusLogAsync(BayStatusLog statusLog);
    Task<BayAllocation?> GetActiveBayAllocationAsync(long bayId);
    Task<BayAllocation> GetBayAllocationWithDetailsAsync(long allocationId);
    Task<IReadOnlyList<Bay>> GetActiveBaysByBranchAsync(long branchId);
    Task<IReadOnlyList<BayAllocation>> GetBayAllocationsForTimelineAsync(long branchId, DateTime startOfDay, DateTime endOfDay);
    Task SaveChangesInTransactionAsync();
}
