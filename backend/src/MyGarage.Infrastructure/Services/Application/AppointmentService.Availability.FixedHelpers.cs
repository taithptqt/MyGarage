using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<List<Appointment>> LoadAppointmentsForDateAsync(long branchId, DateOnly date)
        {
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            return await _context.Appointments
                .AsNoTracking()
                .Where(a => a.BranchId == branchId &&
                            a.StartTime < dayEnd &&
                            a.EndTime > dayStart &&
                            !AppointmentStatusGroups.Closed.Contains(a.Status))
                .ToListAsync();
        }

        private async Task<List<WorkSlot>> LoadOvernightLocksForDateAsync(long branchId, DateOnly date)
        {
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            return await _context.WorkSlots
                .AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.SlotType == nameof(WorkSlotType.REPAIR)
                    && s.Status == nameof(WorkSlotStatus.OVERNIGHT_HOLD)
                    && s.StartTime < dayEnd
                    && s.EndTime > dayStart)
                .ToListAsync();
        }

        private bool ShouldSkipByLeadTime(DateOnly date, DateTime startAt, int minLeadMinutes)
        {
            return date == _clock.LocalToday && startAt < _clock.LocalNow.AddMinutes(minLeadMinutes);
        }

        private AvailableSlotResponseDto? EvaluateFixedSlotCandidate(
            long branchId,
            DateTime current,
            int requiredDuration,
            DateTime openTime,
            IReadOnlyList<Appointment> appointments,
            IReadOnlyList<Appointment> syntheticHoldAppointments,
            List<Bay> activeBays,
            IReadOnlyList<WorkSlot> overnightLocks,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            CapacityProfile capacity,
            ServiceCluster requestedCluster,
            int vehicleCount = 1,
            int slotStepMinutes = 15)
        {
            var end = current.AddMinutes(requiredDuration);
            var overlap = appointments.Where(a => a.StartTime < end && a.EndTime > current).ToList();
            overlap.AddRange(syntheticHoldAppointments.Where(a => a.StartTime < end && a.EndTime > current));
            var locked = CountLockedBays(activeBays, overnightLocks, current, end);
            var effectiveCapacity = new CapacityProfile(
                Math.Max(0, capacity.Quick - locked.lockedQuick),
                Math.Max(0, capacity.Repair - locked.lockedRepair),
                Math.Max(0, capacity.Total - locked.lockedQuick - locked.lockedRepair));

            var configuredLimit = ResolveConfiguredCapacityLimit(
                current,
                end,
                capacityConfigs,
                effectiveCapacity.Total,
                branchId,
                slotStepMinutes);
            effectiveCapacity = ApplyConfiguredCapacityLimit(effectiveCapacity, configuredLimit);

            var effectiveOnlineLimit = ResolveConfiguredOnlineBookingLimit(
                current,
                end,
                capacityConfigs,
                configuredLimit,
                branchId,
                slotStepMinutes);
            var slotEvaluation = EvaluateSlot(overlap, requestedCluster, effectiveCapacity, effectiveOnlineLimit);
            
            // Bo sung kiem tra cho truong hop batch booking
            bool hasEnoughCapacity = slotEvaluation.IsAvailable && (slotEvaluation.RemainingCapacity >= vehicleCount);

            if (effectiveCapacity.Total <= 0 || !hasEnoughCapacity)
            {
                return null;
            }

            var score = CalculateCompactionScore(current, openTime, overlap);
            return new AvailableSlotResponseDto
            {
                StartTime = current,
                EndTime = end,
                MaxCapacity = slotEvaluation.MaxCapacity,
                BookedCount = overlap.Count,
                RemainingCapacity = slotEvaluation.RemainingCapacity,
                IsAvailable = true,
                IsRoundedHour = current.Minute == 0,
                Session = ResolveSession(current.TimeOfDay),
                CompactionScore = score,
                BookingFlow = AppointmentBookingFlows.Fixed,
                EstimatedDurationMinutes = requiredDuration
            };
        }

        private AvailableSlotResponseDto? EvaluateFlexibleSlotCandidate(
            long branchId,
            DateTime current,
            int requiredDuration,
            DateTime openTime,
            IReadOnlyList<Appointment> appointments,
            IReadOnlyList<Appointment> syntheticHoldAppointments,
            List<Bay> activeBays,
            IReadOnlyList<WorkSlot> overnightLocks,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            CapacityProfile capacity,
            int vehicleCount = 1,
            int slotStepMinutes = 15)
        {
            var end = current.AddMinutes(requiredDuration);
            var overlap = appointments.Where(a => a.StartTime < end && a.EndTime > current).ToList();
            overlap.AddRange(syntheticHoldAppointments.Where(a => a.StartTime < end && a.EndTime > current));

            var locked = CountLockedBays(activeBays, overnightLocks, current, end);
            var effectiveCapacity = new CapacityProfile(
                Math.Max(0, capacity.Quick - locked.lockedQuick),
                Math.Max(0, capacity.Repair - locked.lockedRepair),
                Math.Max(0, capacity.Total - locked.lockedQuick - locked.lockedRepair));

            var configuredLimit = ResolveConfiguredCapacityLimit(
                current,
                end,
                capacityConfigs,
                effectiveCapacity.Total,
                branchId,
                slotStepMinutes);
            effectiveCapacity = ApplyConfiguredCapacityLimit(effectiveCapacity, configuredLimit);

            var effectiveOnlineLimit = ResolveConfiguredOnlineBookingLimit(
                current,
                end,
                capacityConfigs,
                configuredLimit,
                branchId,
                slotStepMinutes);
            var receptionistLimit = ResolveFlexibleIntakeCapacity(requiredDuration);
            var effectiveQueueLimit = effectiveOnlineLimit <= 0
                ? 0
                : Math.Min(receptionistLimit, effectiveOnlineLimit);

            if (effectiveCapacity.Total <= 0 || effectiveQueueLimit <= 0)
            {
                return null;
            }

            var bookedCount = overlap.Count;
            var remainingCapacity = Math.Max(0, effectiveQueueLimit - bookedCount);
            if (remainingCapacity < Math.Max(1, vehicleCount))
            {
                return null;
            }

            var score = CalculateCompactionScore(current, openTime, overlap);
            return new AvailableSlotResponseDto
            {
                StartTime = current,
                EndTime = end,
                MaxCapacity = effectiveQueueLimit,
                BookedCount = bookedCount,
                RemainingCapacity = remainingCapacity,
                IsAvailable = true,
                IsRoundedHour = current.Minute == 0,
                Session = ResolveSession(current.TimeOfDay),
                CompactionScore = score,
                BookingFlow = AppointmentBookingFlows.Flexible,
                EstimatedDurationMinutes = requiredDuration
            };
        }
    }
}
