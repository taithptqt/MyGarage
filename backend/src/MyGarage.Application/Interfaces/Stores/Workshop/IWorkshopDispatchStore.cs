using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IWorkshopDispatchStore
{
    Task<IReadOnlyList<ServiceOrder>> GetDispatchServiceOrdersAsync(
        long branchId,
        DateTime dayStart,
        DateTime dayEnd,
        IReadOnlyCollection<string> activeOrderStatuses);

    Task<IReadOnlyList<BayAllocation>> GetBayAllocationsByServiceOrderIdsAsync(IReadOnlyCollection<long> serviceOrderIds);
    Task<IReadOnlyList<ServiceOrderDetail>> GetServiceOrderDetailsByServiceOrderIdsAsync(IReadOnlyCollection<long> serviceOrderIds);
    Task<IReadOnlyList<ServiceStaffAssignment>> GetAssignmentsByDetailIdsAsync(IReadOnlyCollection<long> detailIds);
    Task<Dictionary<long, Staff>> GetTechniciansByIdsAsync(IReadOnlyCollection<long> technicianIds);
    Task<IReadOnlyList<MaterialRequest>> GetMaterialRequestsByServiceOrderIdsAsync(IReadOnlyCollection<long> serviceOrderIds);
    Task<IReadOnlyList<MaterialRequestDetail>> GetMaterialRequestDetailsByRequestIdsAsync(IReadOnlyCollection<long> materialRequestIds);
}
