using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task EnsureFixedFlowCapacityAsync(
            long branchId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            ServiceCluster requestedCluster,
            string? excludeHoldToken = null)
        {
            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();

            var capacity = ComputeCapacity(activeBays);

            var overlapping = await _context.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.BranchId == branchId &&
                    a.StartTime < requestedEndAt &&
                    a.EndTime > requestedStartAt &&
                    !AppointmentStatusGroups.Closed.Contains(a.Status))
                .ToListAsync();
            var activeHolds = _slotHoldService.GetActiveHolds(
                branchId,
                requestedStartAt,
                requestedEndAt,
                AppointmentBookingFlows.Fixed,
                excludeHoldToken);
            var flexibleHolds = _slotHoldService.GetActiveHolds(
                branchId,
                requestedStartAt,
                requestedEndAt,
                AppointmentBookingFlows.Flexible,
                excludeHoldToken);
            overlapping.AddRange(BuildSyntheticAppointmentsFromHolds(activeHolds.Concat(flexibleHolds).ToList()));

            var overnightLocks = await _context.WorkSlots
                .AsNoTracking()
                .Where(s =>
                    s.BranchId == branchId &&
                    s.SlotType == nameof(WorkSlotType.OVERNIGHT_LOCK) &&
                    s.StartTime < requestedEndAt &&
                    s.EndTime > requestedStartAt &&
                    s.Status != nameof(WorkSlotStatus.CANCELED) &&
                    s.Status != AppointmentStatuses.CancelledLegacy)
                .ToListAsync();

            var locked = CountLockedBays(activeBays, overnightLocks, requestedStartAt, requestedEndAt);
            var effectiveCapacity = new CapacityProfile(
                Math.Max(0, capacity.Quick - locked.lockedQuick),
                Math.Max(0, capacity.Repair - locked.lockedRepair),
                Math.Max(0, capacity.Total - locked.lockedQuick - locked.lockedRepair));
            var effectiveOnlineLimit = Math.Max(1, (int)Math.Floor(effectiveCapacity.Total * 0.85m));
            var slotEvaluation = EvaluateSlot(overlapping, requestedCluster, effectiveCapacity, effectiveOnlineLimit);

            if (effectiveCapacity.Total <= 0 || !slotEvaluation.IsAvailable)
                throw new InvalidOperationException("Khung gi\u1edd n\u00e0y \u0111\u00e3 h\u1ebft c\u00f4ng su\u1ea5t theo lo\u1ea1i khoang ho\u1eb7c v\u01b0\u1ee3t ng\u01b0\u1ee1ng 85% online.");
        }

        private async Task EnsureFlexibleQueueCapacityAsync(long branchId, DateTime requestedStartAt, DateTime requestedEndAt, string? excludeHoldToken = null)
        {
            var queueLoad = await _context.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.BranchId == branchId &&
                    a.StartTime < requestedEndAt &&
                    a.EndTime > requestedStartAt &&
                    (a.Status == AppointmentStatuses.Queued ||
                     a.Status == AppointmentStatuses.Arrived ||
                     a.Status == AppointmentStatuses.ArrivedLate))
                .CountAsync();
            var heldCount = _slotHoldService
                .GetActiveHolds(branchId, requestedStartAt, requestedEndAt, AppointmentBookingFlows.Flexible, excludeHoldToken)
                .Sum(h => Math.Max(1, h.VehicleCount));

            if ((queueLoad + heldCount) >= 40)
                throw new InvalidOperationException("Khung check-in \u0111ang qu\u00e1 t\u1ea3i. Vui l\u00f2ng ch\u1ecdn khung kh\u00e1c.");
        }
    }
}
