using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed partial class EfAppointmentStore
{
    public async Task<WorkSlot?> GetWorkSlotByIdAsync(long workSlotId)
    {
        return await _context.WorkSlots.FirstOrDefaultAsync(s => s.Id == workSlotId);
    }

    public async Task<List<WorkSlot>> GetDelayedWorkSlotsAsync(long branchId, DateTime atTime)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.EndTime < atTime
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.COMPLETED)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW))
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetActiveDispatchWorkSlotsAsync(long branchId, DateTime snapshot)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.StartTime <= snapshot
                && (s.ActualReleaseTime == null || s.ActualReleaseTime > snapshot)
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW)
                && s.Status != nameof(WorkSlotStatus.COMPLETED))
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetActiveProactiveDelayWorkSlotsAsync(long branchId, DateTime now, DateTime maxStartTime)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.BayId.HasValue
                && s.StartTime <= maxStartTime
                && (s.ActualReleaseTime == null || s.ActualReleaseTime > now)
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW)
                && s.Status != nameof(WorkSlotStatus.COMPLETED))
            .OrderBy(s => s.BayId)
            .ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<List<long>> GetOccupiedBayIdsAtAsync(long branchId, DateTime now)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.BayId.HasValue
                && s.StartTime <= now
                && (s.ActualReleaseTime ?? s.EndTime) > now
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW)
                && s.Status != nameof(WorkSlotStatus.COMPLETED))
            .Select(s => s.BayId!.Value)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetOvernightLockSlotsAsync(long branchId, DateTime startAt, DateTime endAt)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s =>
                s.BranchId == branchId &&
                s.SlotType == nameof(WorkSlotType.REPAIR) &&
                s.Status == nameof(WorkSlotStatus.OVERNIGHT_HOLD) &&
                s.StartTime < endAt &&
                s.EndTime > startAt)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetProjectedRepairReservationsAsync(
        long branchId,
        DateTime startAt,
        DateTime endAt,
        string marker)
    {
        return await (
                from slot in _context.WorkSlots.AsNoTracking()
                join appointment in _context.Appointments.AsNoTracking()
                    on slot.AppointmentId equals appointment.Id
                where slot.BranchId == branchId
                      && slot.StartTime < endAt
                      && slot.EndTime > startAt
                      && slot.SlotType == nameof(WorkSlotType.REPAIR)
                      && slot.Status != nameof(WorkSlotStatus.CANCELED)
                      && slot.Status != nameof(WorkSlotStatus.NOSHOW)
                      && slot.Status != nameof(WorkSlotStatus.COMPLETED)
                      && slot.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING)
                      && slot.Notes != null
                      && slot.Notes.Contains(marker)
                select new Appointment
                {
                    BranchId = slot.BranchId,
                    CustomerId = appointment.CustomerId,
                    VehicleId = appointment.VehicleId,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    RequestedServiceIds = appointment.RequestedServiceIds,
                    RequestedPackageIds = appointment.RequestedPackageIds,
                    CustomerNotes = appointment.CustomerNotes,
                    Status = appointment.Status
                })
            .ToListAsync();
    }

    public async Task<List<BayTimeRangeSnapshot>> GetBlockingWorkSlotRangesAsync(
        long branchId,
        DateTime scheduleStart,
        DateTime scheduleEnd)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s =>
                s.BranchId == branchId &&
                s.BayId.HasValue &&
                s.StartTime < scheduleEnd &&
                s.EndTime > scheduleStart &&
                s.Status != nameof(WorkSlotStatus.CANCELED) &&
                s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                s.Status != nameof(WorkSlotStatus.COMPLETED) &&
                s.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING))
            .Select(s => new BayTimeRangeSnapshot(s.BayId!.Value, s.StartTime, s.EndTime))
            .ToListAsync();
    }

    public async Task<List<long>> GetBlockingBayIdsAsync(long branchId, DateTime startAt, DateTime endAt)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s =>
                s.BranchId == branchId &&
                s.BayId.HasValue &&
                s.StartTime < endAt &&
                s.EndTime > startAt &&
                s.Status != nameof(WorkSlotStatus.CANCELED) &&
                s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                s.Status != nameof(WorkSlotStatus.COMPLETED) &&
                s.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING))
            .Select(s => s.BayId!.Value)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetActiveWorkSlotsByAppointmentAsync(long appointmentId, bool excludeOvernightHold = false)
    {
        var query = _context.WorkSlots
            .Where(slot =>
                slot.AppointmentId == appointmentId &&
                slot.Status != nameof(WorkSlotStatus.CANCELED) &&
                slot.Status != nameof(WorkSlotStatus.NOSHOW) &&
                slot.Status != nameof(WorkSlotStatus.COMPLETED));

        if (excludeOvernightHold)
        {
            query = query.Where(slot => slot.Status != nameof(WorkSlotStatus.OVERNIGHT_HOLD));
        }

        return await query
            .OrderBy(slot => slot.SequenceOrder)
            .ThenBy(slot => slot.StartTime)
            .ThenBy(slot => slot.Id)
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetCollisionWorkSlotsAsync(
        long branchId,
        long bayId,
        long excludeSlotId,
        DateTime now,
        DateTime proposedEnd)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.BayId == bayId
                && s.Id != excludeSlotId
                && s.StartTime < proposedEnd
                && (s.ActualReleaseTime == null || s.ActualReleaseTime > now)
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW)
                && s.Status != nameof(WorkSlotStatus.COMPLETED))
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetOpenWorkSlotsInWindowAsync(long branchId, DateTime now, DateTime timelineEnd)
    {
        return await _context.WorkSlots
            .AsNoTracking()
            .Where(s => s.BranchId == branchId
                && s.StartTime < timelineEnd
                && (s.ActualReleaseTime == null || s.ActualReleaseTime > now)
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW)
                && s.Status != nameof(WorkSlotStatus.COMPLETED))
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetExpiredLockedWorkSlotsAsync(DateTime releasedAt)
    {
        return await _context.WorkSlots
            .Where(s =>
                s.Status == nameof(WorkSlotStatus.LOCKED) &&
                s.LockedUntil.HasValue &&
                s.LockedUntil.Value <= releasedAt)
            .ToListAsync();
    }

    public async Task<List<WorkSlot>> GetUnfinishedOvernightSourcesAsync(long branchId, DateTime closingTime)
    {
        return await _context.WorkSlots
            .Where(s => s.BranchId == branchId
                && s.BayId.HasValue
                && s.StartTime <= closingTime
                && s.SlotType != nameof(WorkSlotType.BUFFER)
                && s.Status != nameof(WorkSlotStatus.COMPLETED)
                && s.Status != nameof(WorkSlotStatus.CANCELED)
                && s.Status != nameof(WorkSlotStatus.NOSHOW)
                && s.Status != nameof(WorkSlotStatus.OVERNIGHT_HOLD)
                && (s.ActualReleaseTime == null || s.ActualReleaseTime > closingTime))
            .ToListAsync();
    }

    public async Task<bool> HasOvernightLockAsync(
        long branchId,
        long bayId,
        DateTime nextMorning,
        DateTime nextDayEnd,
        string marker)
    {
        return await _context.WorkSlots.AnyAsync(s =>
            s.BranchId == branchId
            && s.BayId == bayId
            && s.SlotType == nameof(WorkSlotType.REPAIR)
            && s.Status == nameof(WorkSlotStatus.OVERNIGHT_HOLD)
            && s.StartTime >= nextMorning
            && s.StartTime < nextDayEnd
            && s.Notes != null
            && s.Notes.Contains(marker));
    }

    public async Task<WorkSlot?> GetOvernightLockByMarkerAsync(long branchId, string marker)
    {
        return await _context.WorkSlots
            .FirstOrDefaultAsync(s => s.BranchId == branchId
                && s.SlotType == nameof(WorkSlotType.REPAIR)
                && s.Status == nameof(WorkSlotStatus.OVERNIGHT_HOLD)
                && s.Notes != null
                && s.Notes.Contains(marker));
    }

    public async Task<List<PartInventoryReservation>> GetActivePartReservationsForSlotsAsync(IReadOnlyCollection<long> slotIds)
    {
        if (slotIds.Count == 0)
        {
            return new List<PartInventoryReservation>();
        }

        return await _context.PartInventoryReservations
            .Where(r =>
                slotIds.Contains(r.WorkSlotId) &&
                r.Status == "RESERVED")
            .ToListAsync();
    }

    public async Task<Dictionary<long, decimal>> GetReservedPartQuantitiesForSlotsAsync(IReadOnlyCollection<long> slotIds)
    {
        if (slotIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        return await _context.PartInventoryReservations
            .AsNoTracking()
            .Where(reservation =>
                slotIds.Contains(reservation.WorkSlotId) &&
                reservation.Status == "RESERVED")
            .GroupBy(reservation => reservation.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                ReservedQuantity = group.Sum(item => item.ReservedQuantity)
            })
            .ToDictionaryAsync(item => item.ProductId, item => item.ReservedQuantity);
    }

    public void AddWorkSlot(WorkSlot slot) => _context.WorkSlots.Add(slot);
    public void AddWorkSlots(IEnumerable<WorkSlot> slots) => _context.WorkSlots.AddRange(slots);
    public void AddPartInventoryReservation(PartInventoryReservation reservation) => _context.PartInventoryReservations.Add(reservation);
}
