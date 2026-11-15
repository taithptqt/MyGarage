using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentWorkSlotStore
{
    Task<WorkSlot?> GetWorkSlotByIdAsync(long workSlotId);
    Task<List<WorkSlot>> GetDelayedWorkSlotsAsync(long branchId, DateTime atTime);
    Task<List<WorkSlot>> GetActiveDispatchWorkSlotsAsync(long branchId, DateTime snapshot);
    Task<List<WorkSlot>> GetActiveProactiveDelayWorkSlotsAsync(long branchId, DateTime now, DateTime maxStartTime);
    Task<List<long>> GetOccupiedBayIdsAtAsync(long branchId, DateTime now);
    Task<List<WorkSlot>> GetOvernightLockSlotsAsync(long branchId, DateTime startAt, DateTime endAt);
    Task<List<Appointment>> GetProjectedRepairReservationsAsync(long branchId, DateTime startAt, DateTime endAt, string marker);
    Task<List<BayTimeRangeSnapshot>> GetBlockingWorkSlotRangesAsync(long branchId, DateTime scheduleStart, DateTime scheduleEnd);
    Task<List<long>> GetBlockingBayIdsAsync(long branchId, DateTime startAt, DateTime endAt);
    Task<List<WorkSlot>> GetActiveWorkSlotsByAppointmentAsync(long appointmentId, bool excludeOvernightHold = false);
    Task<List<WorkSlot>> GetCollisionWorkSlotsAsync(long branchId, long bayId, long excludeSlotId, DateTime now, DateTime proposedEnd);
    Task<List<WorkSlot>> GetOpenWorkSlotsInWindowAsync(long branchId, DateTime now, DateTime timelineEnd);
    Task<List<WorkSlot>> GetExpiredLockedWorkSlotsAsync(DateTime releasedAt);
    Task<List<WorkSlot>> GetUnfinishedOvernightSourcesAsync(long branchId, DateTime closingTime);
    Task<bool> HasOvernightLockAsync(long branchId, long bayId, DateTime nextMorning, DateTime nextDayEnd, string marker);
    Task<WorkSlot?> GetOvernightLockByMarkerAsync(long branchId, string marker);
}
