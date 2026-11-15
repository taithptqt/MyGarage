using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentAvailabilityHelpers;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentAvailabilityService : IAppointmentAvailabilityService
    {
        private const int SlotHoldMinutes = 15;

        private static readonly string[] BookingHealthQueueStatuses =
        {
            nameof(AppointmentStatus.PENDING),
            nameof(AppointmentStatus.CONFIRMED),
            nameof(AppointmentStatus.LATE),
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE)
        };

        private readonly IAppointmentStore _store;
        private readonly ISlotHoldService _slotHoldService;
        private readonly ISystemClock _clock;
        private readonly IAppLogger<AppointmentAvailabilityService> _logger;
        private readonly IAppointmentFlowResolutionService _flowResolution;
        private readonly IAppointmentSlotDiscoveryService _slotDiscovery;

        public AppointmentAvailabilityService(
            IAppointmentStore store,
            ISlotHoldService slotHoldService,
            ISystemClock clock,
            IAppLogger<AppointmentAvailabilityService> logger,
            IAppointmentFlowResolutionService flowResolution,
            IAppointmentSlotDiscoveryService slotDiscovery)
        {
            _store = store;
            _slotHoldService = slotHoldService;
            _clock = clock;
            _logger = logger;
            _flowResolution = flowResolution;
            _slotDiscovery = slotDiscovery;
        }

        public Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(
            long branchId,
            DateOnly date,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = true,
            bool ignoreMinLeadTime = false)
            => _slotDiscovery.GetAvailableSlotsAsync(
                branchId,
                date,
                requestedServiceIds,
                requestedPackageIds,
                bookingFlow,
                vehicleCount,
                isParallel,
                ignoreMinLeadTime);

        public Task<NearestSlotSuggestionResponseDto> SuggestNearestSlotAsync(
            long branchId,
            DateTime requestedCheckInAt,
            string? requestedServiceIds = null,
            string? requestedPackageIds = null,
            string? bookingFlow = null,
            int vehicleCount = 1,
            bool isParallel = false,
            int searchDays = 3)
            => _slotDiscovery.SuggestNearestSlotAsync(
                branchId,
                requestedCheckInAt,
                requestedServiceIds,
                requestedPackageIds,
                bookingFlow,
                vehicleCount,
                isParallel,
                searchDays);

        public Task<BookingFlowType> ResolveFlowAsync(
            string? bookingFlow,
            string? requestedServiceIds,
            string? requestedPackageIds = null)
            => _flowResolution.ResolveFlowAsync(bookingFlow, requestedServiceIds, requestedPackageIds);

        public BookingFlowType ResolveFlowByAppointment(Appointment appointment)
            => _flowResolution.ResolveFlowByAppointment(appointment);

        public Task<string?> ResolveRequestedServiceIdsForBookingAsync(
            string? requestedServiceIds,
            string? requestedPackageIds = null)
            => _flowResolution.ResolveRequestedServiceIdsForBookingAsync(requestedServiceIds, requestedPackageIds);

        public Task<int> ResolveScheduledDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false,
            string? requestedPackageIds = null)
            => _flowResolution.ResolveScheduledDurationAsync(requestedServiceIds, slotStepMinutes, isParallel, requestedPackageIds);

        public int ResolveFlexibleWindowMinutes(int slotStepMinutes)
            => _flowResolution.ResolveFlexibleWindowMinutes(slotStepMinutes);

        public Task<ServiceCluster> ResolveServiceClusterAsync(
            string? requestedServiceIds,
            string? requestedPackageIds,
            string? notes)
            => _flowResolution.ResolveServiceClusterAsync(requestedServiceIds, requestedPackageIds, notes);

        public Task<IReadOnlyList<long>> ResolvePackageItemServiceIdsAsync(string? requestedPackageIds)
            => _flowResolution.ResolvePackageItemServiceIdsAsync(requestedPackageIds);

        public Task<List<Package>> LoadRequestedPackagesAsync(string? requestedPackageIds)
            => _flowResolution.LoadRequestedPackagesAsync(requestedPackageIds);

        public async Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto)
        {
            if (dto.BranchId <= 0)
                throw new InvalidOperationException("Chi nhánh không hợp lệ.");

            var opHour = await _store.GetBranchOperatingHourAsync(dto.BranchId, (int)dto.StartTime.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var flow = await ResolveFlowAsync(dto.BookingFlow, dto.RequestedServiceIds, dto.RequestedPackageIds);
            var plannedDuration = await ResolveScheduledDurationAsync(dto.RequestedServiceIds, slotStepMinutes, false, dto.RequestedPackageIds);

            var holdEndTime = dto.EndTime;

            if (flow == BookingFlowType.Flexible && holdEndTime <= dto.StartTime)
            {
                var flexibleWindow = ResolveFlexibleWindowMinutes(slotStepMinutes);
                holdEndTime = dto.StartTime.AddMinutes(flexibleWindow);
            }

            if (flow == BookingFlowType.Fixed && holdEndTime <= dto.StartTime)
            {
                holdEndTime = dto.StartTime.AddMinutes(plannedDuration);
            }

            if (holdEndTime <= dto.StartTime)
                throw new InvalidOperationException("Khung giờ giữ chỗ không hợp lệ.");

            var hold = _slotHoldService.CreateHold(
                dto.BranchId,
                dto.StartTime,
                holdEndTime,
                flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed,
                dto.RequestedServiceIds,
                dto.RequestedPackageIds,
                Math.Max(1, dto.VehicleCount));

            return new SlotHoldResponseDto
            {
                HoldToken = hold.Token,
                ExpiresAt = hold.ExpiresAt,
                HoldMinutes = SlotHoldMinutes
            };
        }

        public Task<bool> ReleaseSlotHoldAsync(string holdToken)
        {
            var released = _slotHoldService.ReleaseHold(holdToken);
            return Task.FromResult(released);
        }

        public List<Appointment> BuildSyntheticAppointmentsFromHolds(IReadOnlyList<SlotHoldRecord> holds)
            => AppointmentAvailabilityHelpers.BuildSyntheticAppointmentsFromHolds(holds);

        public void ValidateSlotHoldOrThrow(
            BookAppointmentRequestDto dto,
            BookingFlowType flow,
            DateTime requestedStartAt,
            DateTime requestedEndAt)
        {
            if (string.IsNullOrWhiteSpace(dto.SlotHoldToken))
            {
                return;
            }

            if (!_slotHoldService.TryGetActiveHold(dto.SlotHoldToken, out var hold) || hold == null)
            {
                throw new InvalidOperationException("Giữ chỗ đã hết hạn. Vui lòng chọn lại khung giờ.");
            }

            var expectedFlow = flow == BookingFlowType.Flexible ? AppointmentBookingFlows.Flexible : AppointmentBookingFlows.Fixed;
            if (hold.BranchId != dto.BranchId ||
                !string.Equals(hold.BookingFlow, expectedFlow, StringComparison.OrdinalIgnoreCase) ||
                hold.StartTime != requestedStartAt)
            {
                throw new InvalidOperationException("Thông tin giữ chỗ không khớp với lịch đang đặt.");
            }

            if (flow == BookingFlowType.Fixed && requestedEndAt > hold.EndTime)
            {
                throw new InvalidOperationException("Khung giờ giữ chỗ không còn phù hợp với lịch đang đặt. Vui lòng chọn lại khung giờ.");
            }
        }

        public void ConsumeSlotHold(string? holdToken)
        {
            if (!string.IsNullOrWhiteSpace(holdToken))
            {
                _slotHoldService.ConsumeHold(holdToken);
            }
        }

        public async Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null)
        {
            var atTime = at ?? _clock.LocalNow;
            var bays = await _store.GetActiveBaysAsync(branchId);
            var capacity = ComputeCapacity(bays);
            var delayThreshold = Math.Max(120, capacity.Total * 15);
            var parkingThreshold = Math.Max(12, (int)Math.Ceiling(capacity.Total * 1.5));

            var delayedSlots = await _store.GetDelayedWorkSlotsAsync(branchId, atTime);

            var totalDelay = delayedSlots.Sum(s => (int)Math.Max(0, Math.Ceiling(((s.ActualReleaseTime ?? atTime) - s.EndTime).TotalMinutes)));
            var queueCount = await _store.CountActiveBranchAppointmentsAsync(branchId, BookingHealthQueueStatuses);

            var locked = totalDelay >= delayThreshold || (totalDelay >= delayThreshold / 2 && queueCount >= parkingThreshold);
            return new BranchBookingHealthResponseDto
            {
                BranchId = branchId,
                EvaluatedAt = atTime,
                IsBookingLocked = locked,
                LockUntil = locked ? atTime.AddHours(BookingLockHours) : null,
                TotalDelayMinutes = totalDelay,
                DelayThresholdMinutes = delayThreshold,
                ParkingQueueCount = queueCount,
                ParkingThreshold = parkingThreshold,
                Message = locked ? "Chi nhanh dang qua tai, tam khoa nhan lich 4 gio." : "Chi nhanh dang trong nguong an toan.",
                SuggestedBranches = locked ? await GetAlternativeBranchesAsync(branchId, atTime) : new List<BranchSuggestionDto>()
            };
        }

        private async Task<List<BranchSuggestionDto>> GetAlternativeBranchesAsync(long currentBranchId, DateTime at)
        {
            var current = await _store.GetActiveBranchByIdAsync(currentBranchId);
            var branches = await _store.GetActiveBranchesExceptAsync(currentBranchId);
            var output = new List<BranchSuggestionDto>();

            foreach (var branch in branches)
            {
                var bayCount = await _store.CountActiveBaysAsync(branch.Id);
                if (bayCount <= 0) bayCount = 20;

                var active = await _store.CountOverlappingNonClosedAppointmentsAsync(branch.Id, at, at.AddMinutes(15));
                var utilization = Math.Min(100d, active * 100d / bayCount);
                if (utilization >= 85d) continue;

                output.Add(new BranchSuggestionDto
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    DistanceKm = HaversineDistanceKm(current?.Lat, current?.Lng, branch.Lat, branch.Lng),
                    EstimatedUtilizationPct = Math.Round(utilization, 1),
                    EarliestAvailableAt = at.AddMinutes(30)
                });
            }

            return output.OrderBy(b => b.DistanceKm).ThenBy(b => b.EstimatedUtilizationPct).Take(3).ToList();
        }
    }
}
