using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisOrderContextStore
{
    int? GetBranchTeamLeaderWarningMinutes(long branchId);
    int? GetDefaultTeamLeaderWarningMinutes();
    int? GetBranchTeamLeaderEscalationMinutes(long branchId);
    int? GetDefaultTeamLeaderEscalationMinutes();
    IReadOnlyList<string> GetConfiguredDiagnoseBayCodes(long branchId);

    Task<ServiceOrder?> GetServiceOrderByIdAsync(long serviceOrderId);
    Task<bool> HasSignedCheckInAsync(long serviceOrderId, string logTypeCheckIn);
    Task<IReadOnlyList<DiagnosisServiceOrderBoardRow>> GetServiceOrderBoardRowsAsync(IReadOnlyCollection<long> serviceOrderIds, long? branchId);
    Task<DiagnosisDispatchServiceOrderInfo?> GetDispatchServiceOrderInfoAsync(long serviceOrderId);
    Task<DiagnosisIntakeServiceOrderInfo?> GetIntakeServiceOrderInfoAsync(long serviceOrderId);
    Task<DiagnosisQueueServiceOrderInfo?> GetQueueServiceOrderInfoAsync(long serviceOrderId);
    Task<string?> GetServiceOrderCategoryAsync(long serviceOrderId);
    Task<DateTime?> GetLastVehicleVisitAtAsync(long vehicleId, long excludeServiceOrderId);
    Task<bool> HasRecentReworkOrWarrantyAsync(long vehicleId, long excludeServiceOrderId, DateTime since);
    Task<IReadOnlyDictionary<long, long>> GetServiceOrderExecuteBranchMapAsync(IReadOnlyCollection<long> orderIds);
}
