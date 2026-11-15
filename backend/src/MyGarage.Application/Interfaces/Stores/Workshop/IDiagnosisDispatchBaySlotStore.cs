using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisDispatchBaySlotStore
{
    Task<bool> IsBayInBranchAsync(long bayId, long branchId);
    Task<long?> FindValidConfiguredBayIdAsync(long bayId, long branchId, bool hasConfiguredCodes, IReadOnlyCollection<string> configuredCodes);
    Task<IReadOnlyList<DiagnosisBayRow>> GetActiveBranchBaysAsync(long branchId);
    Task<IReadOnlyList<DiagnosisBayIdCodeNameRow>> GetActiveDiagnoseBaysAsync(long branchId, string bayTypeSpDiagnose);
    Task<DiagnosisBayDetailRow?> GetBayDetailByIdAsync(long bayId);
    Task<IReadOnlyList<DiagnosisBayAvailabilityRow>> GetActiveBranchBayAvailabilityAsync(long branchId);
    Task<IReadOnlyDictionary<long, string>> GetBayCodeMapAsync(IReadOnlyCollection<long> bayIds);

    Task<IReadOnlyList<WorkSlot>> GetDiagnoseWorkSlotsForOrdersAsync(IReadOnlyCollection<long> serviceOrderIds);
    Task<IReadOnlyList<WorkSlot>> GetActiveDiagnoseWorkSlotsForOrderAsync(long serviceOrderId);
    Task<WorkSlot?> GetLatestActiveDiagnoseWorkSlotAsync(long serviceOrderId);
    Task<IReadOnlyList<DiagnosisBlockingWorkSlotRow>> GetBlockingWorkSlotsForBaysAsync(IReadOnlyCollection<long> bayIds, long excludeSlotId);

    Task<DiagnosisNextBookingRow?> GetNextOpenAppointmentAsync(long branchId, DateTime checkInTime, IReadOnlyCollection<string> closedStatuses);
}
