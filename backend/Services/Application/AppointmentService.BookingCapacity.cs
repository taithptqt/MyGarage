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
            overlapping.AddRange(await LoadProjectedRepairReservationsAsync(branchId, requestedStartAt, requestedEndAt));
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
                    s.SlotType == nameof(WorkSlotType.REPAIR) &&
                    s.Status == nameof(WorkSlotStatus.OVERNIGHT_HOLD) &&
                    s.StartTime < requestedEndAt &&
                    s.EndTime > requestedStartAt)
                .ToListAsync();

            var locked = CountLockedBays(activeBays, overnightLocks, requestedStartAt, requestedEndAt);
            var effectiveCapacity = new CapacityProfile(
                Math.Max(0, capacity.Quick - locked.lockedQuick),
                Math.Max(0, capacity.Repair - locked.lockedRepair),
                Math.Max(0, capacity.Total - locked.lockedQuick - locked.lockedRepair));

            var capacityConfigs = await LoadCapacityConfigsForDayAsync(branchId, DateOnly.FromDateTime(requestedStartAt));
            var configuredLimit = ResolveConfiguredCapacityLimit(
                requestedStartAt,
                requestedEndAt,
                capacityConfigs,
                effectiveCapacity.Total,
                branchId);
            effectiveCapacity = ApplyConfiguredCapacityLimit(effectiveCapacity, configuredLimit);

            var effectiveOnlineLimit = ResolveConfiguredOnlineBookingLimit(
                requestedStartAt,
                requestedEndAt,
                capacityConfigs,
                configuredLimit,
                branchId);
            var slotEvaluation = EvaluateSlot(overlapping, requestedCluster, effectiveCapacity, effectiveOnlineLimit);

            if (effectiveCapacity.Total <= 0 || !slotEvaluation.IsAvailable)
                throw new InvalidOperationException("Khung giờ này đã hết công suất theo cấu hình năng lực khoang.");
        }

        private async Task EnsureFlexibleQueueCapacityAsync(long branchId, DateTime requestedStartAt, DateTime requestedEndAt, string? excludeHoldToken = null)
        {
            var windowMinutes = Math.Max(
                StandardCheckInSetupMinutes,
                (int)Math.Ceiling((requestedEndAt - requestedStartAt).TotalMinutes));
            var receptionistLimit = ResolveFlexibleIntakeCapacity(windowMinutes);

            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();
            var capacity = ComputeCapacity(activeBays);
            var capacityConfigs = await LoadCapacityConfigsForDayAsync(branchId, DateOnly.FromDateTime(requestedStartAt));
            var configuredLimit = ResolveConfiguredCapacityLimit(
                requestedStartAt,
                requestedEndAt,
                capacityConfigs,
                capacity.Total,
                branchId);
            var effectiveOnlineLimit = ResolveConfiguredOnlineBookingLimit(
                requestedStartAt,
                requestedEndAt,
                capacityConfigs,
                configuredLimit,
                branchId);
            var queueLimit = effectiveOnlineLimit <= 0
                ? 0
                : Math.Min(receptionistLimit, effectiveOnlineLimit);

            var queueLoad = await _context.Appointments
                .AsNoTracking()
                .Where(a =>
                    a.BranchId == branchId &&
                    a.StartTime < requestedEndAt &&
                    a.EndTime > requestedStartAt &&
                    (a.Status == nameof(AppointmentStatus.PENDING) ||
                     a.Status == nameof(AppointmentStatus.ARRIVED) ||
                     a.Status == nameof(AppointmentStatus.ARRIVED_LATE) ||
                     a.Status == nameof(AppointmentStatus.CONFIRMED) ||
                     a.Status == nameof(AppointmentStatus.LATE)))
                .CountAsync();
            var heldCount = _slotHoldService
                .GetActiveHolds(branchId, requestedStartAt, requestedEndAt, AppointmentBookingFlows.Flexible, excludeHoldToken)
                .Sum(h => Math.Max(1, h.VehicleCount));

            if (queueLimit <= 0 || (queueLoad + heldCount) >= queueLimit)
                throw new InvalidOperationException("Khung check-in \u0111ang qu\u00e1 t\u1ea3i. Vui l\u00f2ng ch\u1ecdn khung kh\u00e1c.");
        }

        private static int ResolveFlexibleIntakeCapacity(int windowMinutes)
        {
            var effectiveWindow = Math.Max(StandardCheckInSetupMinutes, windowMinutes);
            var slotsPerReceptionist = Math.Max(1, effectiveWindow / StandardCheckInSetupMinutes);
            return Math.Max(1, FlexibleReceptionistCount * slotsPerReceptionist);
        }
    }
}
