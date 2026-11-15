using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentAvailabilityHelpers;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentSlotDiscoveryService : IAppointmentSlotDiscoveryService
    {
        private readonly IAppointmentStore _store;
        private readonly ISlotHoldService _slotHoldService;
        private readonly ISystemClock _clock;
        private readonly IAppointmentFlowResolutionService _flowResolution;

        public AppointmentSlotDiscoveryService(
            IAppointmentStore store,
            ISlotHoldService slotHoldService,
            ISystemClock clock,
            IAppointmentFlowResolutionService flowResolution)
        {
            _store = store;
            _slotHoldService = slotHoldService;
            _clock = clock;
            _flowResolution = flowResolution;
        }

        public async Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(
            long branchId,
            DateOnly date,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = true,
            bool ignoreMinLeadTime = false)
        {
            if (date < _clock.LocalToday)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var flow = await _flowResolution.ResolveFlowAsync(bookingFlow, requestedServiceIds, requestedPackageIds);
            var dayOfWeek = (int)date.DayOfWeek;

            var opHour = await _store.GetBranchOperatingHourAsync(branchId, dayOfWeek);

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
            if (opHour?.IsDayOff == true)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var exception = await _store.GetBranchExceptionAsync(branchId, date);

            if (exception?.IsClosed == true) return Enumerable.Empty<AvailableSlotResponseDto>();
            if (exception?.StartTime.HasValue == true) openTime = exception.StartTime.Value;
            if (exception?.EndTime.HasValue == true) closeTime = exception.EndTime.Value;

            var effectiveRequestedServiceIds = requestedServiceIds;
            var effectiveRequestedPackageIds = requestedPackageIds;
            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds))
            {
                effectiveRequestedServiceIds = await _flowResolution.ResolveRequestedServiceIdsForBookingAsync(null, effectiveRequestedPackageIds);
            }

            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds) && string.IsNullOrWhiteSpace(effectiveRequestedPackageIds))
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var effectiveVehicleCount = Math.Max(1, vehicleCount);
            var useParallel = isParallel;

            var durationBreakdown = await _flowResolution.ResolveDurationBreakdownAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                useParallel,
                effectiveRequestedPackageIds);
            var plannedDuration = durationBreakdown.TotalMinutes;

            if (flow == BookingFlowType.Fixed &&
                ShouldFallbackToFlexibleFlowForLongDuration(plannedDuration, openTime, closeTime))
            {
                flow = BookingFlowType.Flexible;
            }

            var slotWindowDuration = flow == BookingFlowType.Flexible
                ? _flowResolution.ResolveFlexibleWindowMinutes(slotStepMinutes)
                : plannedDuration;

            var slots = await BuildScheduledSlotsAsync(
                branchId,
                date,
                openTime,
                closeTime,
                slotStepMinutes,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds,
                flow,
                slotWindowDuration,
                effectiveVehicleCount,
                ignoreMinLeadTime);

            if (!slots.Any() &&
                flow == BookingFlowType.Fixed &&
                useParallel &&
                HasGroupedRequestedServices(effectiveRequestedServiceIds, effectiveRequestedPackageIds))
            {
                durationBreakdown = await _flowResolution.ResolveDurationBreakdownAsync(
                    effectiveRequestedServiceIds,
                    slotStepMinutes,
                    false,
                    effectiveRequestedPackageIds);
                plannedDuration = durationBreakdown.TotalMinutes;
                slotWindowDuration = plannedDuration;

                slots = await BuildScheduledSlotsAsync(
                    branchId,
                    date,
                    openTime,
                    closeTime,
                    slotStepMinutes,
                    effectiveRequestedServiceIds,
                    effectiveRequestedPackageIds,
                    flow,
                    slotWindowDuration,
                    effectiveVehicleCount,
                    ignoreMinLeadTime);
            }

            var dynamicNotice = flow == BookingFlowType.Flexible
                ? BuildFlexibleDynamicNotice(
                    durationBreakdown.ServiceMinutes,
                    durationBreakdown.SetupMinutes,
                    durationBreakdown.BufferMinutes,
                    plannedDuration)
                : null;

            foreach (var slot in slots)
            {
                slot.BookingFlow = flow == BookingFlowType.Flexible
                    ? AppointmentBookingFlows.Flexible
                    : AppointmentBookingFlows.Fixed;
                slot.EstimatedDurationMinutes = plannedDuration;
                slot.ServiceMinutes = durationBreakdown.ServiceMinutes;
                slot.SetupMinutes = durationBreakdown.SetupMinutes;
                slot.BufferMinutes = durationBreakdown.BufferMinutes;
                slot.DynamicNotice = dynamicNotice;
            }

            return slots.OrderBy(s => s.StartTime);
        }

        public async Task<NearestSlotSuggestionResponseDto> SuggestNearestSlotAsync(
            long branchId,
            DateTime requestedCheckInAt,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = false,
            int searchDays = 3)
        {
            const int suggestionLimit = 5;

            if (branchId <= 0)
            {
                throw new InvalidOperationException("Chi nhánh không hợp lệ.");
            }

            var baselineCheckInAt = requestedCheckInAt < _clock.LocalNow
                ? _clock.LocalNow
                : requestedCheckInAt;

            var effectiveRequestedServiceIds = requestedServiceIds;
            var effectiveRequestedPackageIds = requestedPackageIds;
            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds))
            {
                effectiveRequestedServiceIds = await _flowResolution.ResolveRequestedServiceIdsForBookingAsync(null, effectiveRequestedPackageIds);
            }

            if (string.IsNullOrWhiteSpace(effectiveRequestedServiceIds) && string.IsNullOrWhiteSpace(effectiveRequestedPackageIds))
            {
                return new NearestSlotSuggestionResponseDto
                {
                    BranchId = branchId,
                    RequestedCheckInAt = baselineCheckInAt,
                    RequestedServiceIds = requestedServiceIds,
                    RequestedPackageIds = requestedPackageIds,
                    Message = "Chưa xác định được dịch vụ để gợi ý khung giờ."
                };
            }

            var resolvedFlow = await _flowResolution.ResolveFlowAsync(bookingFlow, effectiveRequestedServiceIds, effectiveRequestedPackageIds);
            var effectiveBookingFlow = resolvedFlow == BookingFlowType.Flexible
                ? AppointmentBookingFlows.Flexible
                : AppointmentBookingFlows.Fixed;

            var opHour = await _store.GetBranchOperatingHourAsync(branchId, (int)baselineCheckInAt.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
            var breakdown = await _flowResolution.ResolveDurationBreakdownAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                isParallel,
                effectiveRequestedPackageIds);

            var candidateSlots = new List<AvailableSlotResponseDto>();
            var effectiveSearchDays = Math.Clamp(searchDays, 1, 7);
            var startDate = DateOnly.FromDateTime(baselineCheckInAt);
            var effectiveVehicleCount = Math.Max(1, vehicleCount);

            for (var i = 0; i < effectiveSearchDays; i++)
            {
                var date = startDate.AddDays(i);
                var slots = (await GetAvailableSlotsAsync(
                        branchId,
                        date,
                        effectiveRequestedServiceIds,
                        effectiveRequestedPackageIds,
                        effectiveBookingFlow,
                        effectiveVehicleCount,
                        isParallel,
                        ignoreMinLeadTime: true))
                    .ToList();

                if (slots.Count == 0)
                {
                    continue;
                }

                foreach (var slot in slots)
                {
                    if (slot.StartTime < baselineCheckInAt)
                    {
                        continue;
                    }

                    slot.ServiceMinutes = breakdown.ServiceMinutes;
                    slot.SetupMinutes = breakdown.SetupMinutes;
                    slot.BufferMinutes = breakdown.BufferMinutes;
                    slot.EstimatedDurationMinutes = breakdown.TotalMinutes;
                    candidateSlots.Add(slot);
                }

                if (candidateSlots.Count >= suggestionLimit)
                {
                    break;
                }
            }

            var ordered = candidateSlots
                .OrderBy(s => s.StartTime)
                .ToList();
            var best = ordered.FirstOrDefault();

            if (best == null)
            {
                return new NearestSlotSuggestionResponseDto
                {
                    BranchId = branchId,
                    RequestedCheckInAt = baselineCheckInAt,
                    RequestedServiceIds = requestedServiceIds,
                    RequestedPackageIds = requestedPackageIds,
                    ServiceMinutes = breakdown.ServiceMinutes,
                    SetupMinutes = breakdown.SetupMinutes,
                    BufferMinutes = breakdown.BufferMinutes,
                    EstimatedDurationMinutes = breakdown.TotalMinutes,
                    Message = "Không tìm thấy khung giờ trống phù hợp trong khoảng tìm kiếm hiện tại."
                };
            }

            var deltaMinutes = (int)Math.Ceiling((best.StartTime - baselineCheckInAt).TotalMinutes);
            var isExactOrEarlier = deltaMinutes <= 0;

            var message = isExactOrEarlier
                ? "Khung giờ xe vào xưởng đang khả dụng ngay."
                : $"Khung giờ phù hợp gần nhất lệch {deltaMinutes} phút so với giờ nhập.";

            return new NearestSlotSuggestionResponseDto
            {
                BranchId = branchId,
                RequestedCheckInAt = baselineCheckInAt,
                RequestedServiceIds = requestedServiceIds,
                RequestedPackageIds = requestedPackageIds,
                ServiceMinutes = breakdown.ServiceMinutes,
                SetupMinutes = breakdown.SetupMinutes,
                BufferMinutes = breakdown.BufferMinutes,
                EstimatedDurationMinutes = breakdown.TotalMinutes,
                SuggestedStartTime = best.StartTime,
                SuggestedEndTime = best.EndTime,
                SuggestionDeltaMinutes = Math.Max(0, deltaMinutes),
                IsExactOrEarlier = isExactOrEarlier,
                Message = message,
                Alternatives = ordered.Take(suggestionLimit).ToList()
            };
        }

        private async Task<List<AvailableSlotResponseDto>> BuildScheduledSlotsAsync(
            long branchId,
            DateOnly date,
            TimeOnly openTime,
            TimeOnly closeTime,
            int slotStepMinutes,
            string? effectiveRequestedServiceIds,
            string? effectiveRequestedPackageIds,
            BookingFlowType flow,
            int requiredDurationMinutes,
            int vehicleCount = 1,
            bool ignoreMinLeadTime = false)
        {
            var activeBays = await _store.GetActiveBaysAsync(branchId);
            var capacity = ComputeCapacity(activeBays);
            var normalizedDayOfWeek = ToCapacityConfigDayOfWeek(date.DayOfWeek);
            var capacityConfigs = await _store.GetActiveCapacityConfigsForDayAsync(branchId, normalizedDayOfWeek);

            var requestedCluster = ServiceCluster.Quick;
            if (flow == BookingFlowType.Fixed)
            {
                requestedCluster = await _flowResolution.ResolveServiceClusterAsync(
                    effectiveRequestedServiceIds,
                    effectiveRequestedPackageIds,
                    null);
            }

            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var appointments = await _store.GetOpenAppointmentsOverlappingAsync(branchId, dayStart, dayEnd);
            if (flow == BookingFlowType.Fixed)
            {
                appointments.AddRange(await _store.GetProjectedRepairReservationsAsync(
                    branchId,
                    dayStart,
                    dayEnd,
                    FlexiblePreallocatedRepairSlotMarker));
            }
            var overnightLocks = await _store.GetOvernightLockSlotsAsync(branchId, dayStart, dayEnd);
            var fixedHolds = _slotHoldService.GetActiveHolds(branchId, dayStart, dayEnd, AppointmentBookingFlows.Fixed);
            var flexibleHolds = _slotHoldService.GetActiveHolds(branchId, dayStart, dayEnd, AppointmentBookingFlows.Flexible);
            var mergedHolds = fixedHolds.Concat(flexibleHolds).ToList();
            var syntheticHoldAppointments = BuildSyntheticAppointmentsFromHolds(mergedHolds);

            var slots = new List<AvailableSlotResponseDto>();
            var current = date.ToDateTime(openTime);
            var lastStart = date.ToDateTime(closeTime).AddMinutes(-requiredDurationMinutes);
            var minLeadMinutes = ignoreMinLeadTime ? 0 : Math.Max(15, slotStepMinutes);
            while (current <= lastStart)
            {
                if (ShouldSkipByLeadTime(date, current, minLeadMinutes))
                {
                    current = current.AddMinutes(slotStepMinutes);
                    continue;
                }

                var candidate = flow == BookingFlowType.Flexible
                    ? EvaluateFlexibleSlotCandidate(
                        branchId,
                        current,
                        requiredDurationMinutes,
                        date.ToDateTime(openTime),
                        appointments,
                        syntheticHoldAppointments,
                        activeBays,
                        overnightLocks,
                        capacityConfigs,
                        capacity,
                        vehicleCount,
                        slotStepMinutes)
                    : EvaluateFixedSlotCandidate(
                        branchId,
                        current,
                        requiredDurationMinutes,
                        date.ToDateTime(openTime),
                        appointments,
                        syntheticHoldAppointments,
                        activeBays,
                        overnightLocks,
                        capacityConfigs,
                        capacity,
                        requestedCluster,
                        vehicleCount,
                        slotStepMinutes);

                if (candidate != null)
                {
                    slots.Add(candidate);
                }

                current = current.AddMinutes(slotStepMinutes);
            }

            MarkTopPicks(slots);
            return slots;
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

        private bool ShouldSkipByLeadTime(DateOnly date, DateTime startAt, int minLeadMinutes)
        {
            return date == _clock.LocalToday && startAt < _clock.LocalNow.AddMinutes(minLeadMinutes);
        }
    }
}
