using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Common;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentBaySelectionService : IAppointmentBaySelectionService
    {
        private readonly IAppointmentStore _store;
        private readonly ISlotHoldService _slotHoldService;
        private readonly IAppointmentSlotHoldOperations _slotHoldOperations;
        private readonly IAppointmentPackageResolver _packageResolver;
        private readonly IAppointmentPreallocationService _preallocationService;
        private readonly IAppointmentPartReservationService _partReservationService;

        private const int StandardCheckInSetupMinutes = AppointmentSchedulingHelpers.StandardCheckInSetupMinutes;
        private const int FlexibleReceptionistCount = AppointmentSchedulingHelpers.FlexibleReceptionistCount;

        private static readonly string[] FlexibleQueueLoadStatuses =
        {
            nameof(AppointmentStatus.PENDING),
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE),
            nameof(AppointmentStatus.CONFIRMED),
            nameof(AppointmentStatus.LATE)
        };

        public AppointmentBaySelectionService(
            IAppointmentStore store,
            ISlotHoldService slotHoldService,
            IAppointmentSlotHoldOperations slotHoldOperations,
            IAppointmentPackageResolver packageResolver,
            IAppointmentPreallocationService preallocationService,
            IAppointmentPartReservationService partReservationService)
        {
            _store = store;
            _slotHoldService = slotHoldService;
            _slotHoldOperations = slotHoldOperations;
            _packageResolver = packageResolver;
            _preallocationService = preallocationService;
            _partReservationService = partReservationService;
        }

        public async Task EnsureRequestedServicesCompatibleWithVehicleEngineAsync(
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds)
        {
            var resolvedVehicleModelId = vehicleModelId.GetValueOrDefault();
            if (resolvedVehicleModelId <= 0)
            {
                return;
            }

            var vehicleFuelType = await _store.GetVehicleModelFuelTypeAsync(resolvedVehicleModelId);

            if (EngineTypeCompatibilityRules.ParseVehicleEngineFamilies(vehicleFuelType).Count == 0)
            {
                return;
            }

            var requestedServices = await LoadRequestedServicesWithPackageItemsAsync(requestedServiceIds, requestedPackageIds);
            var incompatibleServices = requestedServices
                .Where(service => !EngineTypeCompatibilityRules.IsServiceCompatibleWithVehicleFuel(service.EngineType, vehicleFuelType))
                .Select(service => service.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (incompatibleServices.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Dịch vụ không phù hợp với loại động cơ của dòng xe đã chọn: {string.Join(", ", incompatibleServices)}.");
        }

        public Task<PartAvailabilityEvaluation> EvaluatePartAvailabilityForBookingAsync(
            long branchId,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            return _partReservationService.EvaluatePartAvailabilityForBookingAsync(
                branchId,
                vehicleModelId,
                requestedServiceIds,
                requestedPackageIds);
        }

        public async Task EnsureFixedFlowCapacityAsync(
            long branchId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            ServiceCluster requestedCluster,
            string? excludeHoldToken = null,
            IReadOnlyCollection<string>? processedHoldTokens = null)
        {
            var activeBays = await _store.GetActiveBaysAsync(branchId);
            var capacity = ComputeCapacity(activeBays);

            var overlapping = await _store.GetOpenAppointmentsOverlappingAsync(branchId, requestedStartAt, requestedEndAt);
            overlapping.AddRange(await _preallocationService.LoadProjectedRepairReservationsAsync(branchId, requestedStartAt, requestedEndAt));
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
            var processed = processedHoldTokens?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            overlapping.AddRange(_slotHoldOperations.BuildSyntheticAppointmentsFromHolds(activeHolds.Concat(flexibleHolds).Where(hold => !processed.Contains(hold.Token)).ToList()));

            var overnightLocks = await _store.GetOvernightLockSlotsAsync(branchId, requestedStartAt, requestedEndAt);

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

        public async Task EnsureFlexibleQueueCapacityAsync(
            long branchId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            string? excludeHoldToken = null,
            IReadOnlyCollection<string>? processedHoldTokens = null)
        {
            var windowMinutes = Math.Max(
                StandardCheckInSetupMinutes,
                (int)Math.Ceiling((requestedEndAt - requestedStartAt).TotalMinutes));
            var receptionistLimit = ResolveFlexibleIntakeCapacity(windowMinutes);

            var activeBays = await _store.GetActiveBaysAsync(branchId);
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

            var queueLoad = await _store.CountOverlappingAppointmentsWithStatusesAsync(
                branchId,
                requestedStartAt,
                requestedEndAt,
                FlexibleQueueLoadStatuses);
            var processed = processedHoldTokens?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            var heldCount = _slotHoldService
                .GetActiveHolds(branchId, requestedStartAt, requestedEndAt, AppointmentBookingFlows.Flexible, excludeHoldToken)
                .Where(hold => !processed.Contains(hold.Token))
                .Sum(h => Math.Max(1, h.VehicleCount));

            if (queueLimit <= 0 || (queueLoad + heldCount) >= queueLimit)
                throw new InvalidOperationException("Khung check-in \u0111ang qu\u00e1 t\u1ea3i. Vui l\u00f2ng ch\u1ecdn khung kh\u00e1c.");
        }

        public async Task ApplyBookingReservationArtifactsAsync(
            Appointment appointment,
            BookingFlowType flow,
            ServiceCluster requestedCluster,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds,
            long? actorStaffId,
            int plannedDurationMinutes,
            int slotStepMinutes,
            PartAvailabilityEvaluation partAvailability)
        {
            var baySelectionPolicy = await ResolveBaySelectionPolicyAsync(
                requestedServiceIds,
                requestedCluster,
                requestedPackageIds);
            var isDiagnoseOnly = baySelectionPolicy.IsDiagnoseOnly;
            var primarySlot = AppointmentBaySelectionPolicy.CreatePrimaryBookingWorkSlot(
                appointment,
                flow,
                isDiagnoseOnly);

            if (flow == BookingFlowType.Fixed || isDiagnoseOnly)
            {
                primarySlot.BayId = await ResolveAvailableBayIdForBookingAsync(
                    appointment.BranchId,
                    appointment.StartTime,
                    appointment.EndTime,
                    baySelectionPolicy);
                primarySlot.SequenceOrder = isDiagnoseOnly ? 1 : 2;
            }

            _store.AddWorkSlot(primarySlot);
            await _store.SaveChangesInTransactionAsync();

            var partReservationSlot = primarySlot;
            if (flow == BookingFlowType.Flexible && !isDiagnoseOnly)
            {
                var checkInMinutes = Math.Max(
                    slotStepMinutes,
                    (int)Math.Ceiling((appointment.EndTime - appointment.StartTime).TotalMinutes));
                var repairMinutes = Math.Max(slotStepMinutes, plannedDurationMinutes - checkInMinutes);
                var preallocatedSlots = await _preallocationService.BuildFlexibleRepairPreallocatedSlotsAsync(
                    appointment,
                    primarySlot,
                    baySelectionPolicy.RequestedCluster,
                    baySelectionPolicy.OrderedBayTypes,
                    AppointmentBaySelectionPolicy.ResolveTargetBayAreaTag(baySelectionPolicy.PrimaryArea),
                    repairMinutes,
                    slotStepMinutes);

                if (preallocatedSlots.Count > 0)
                {
                    _store.AddWorkSlots(preallocatedSlots);
                    await _store.SaveChangesInTransactionAsync();
                    partReservationSlot = preallocatedSlots[0];
                }
            }

            await _partReservationService.ApplyOrMarkPartsReservationAsync(
                appointment,
                partReservationSlot,
                vehicleModelId,
                requestedServiceIds,
                requestedPackageIds,
                actorStaffId,
                partAvailability);
        }

        private async Task<BaySelectionPolicy> ResolveBaySelectionPolicyAsync(
            string? requestedServiceIds,
            ServiceCluster requestedCluster,
            string? requestedPackageIds = null)
        {
            var services = await LoadRequestedServicesWithPackageItemsAsync(requestedServiceIds, requestedPackageIds);
            var packages = await _packageResolver.LoadRequestedPackagesAsync(requestedPackageIds);
            return AppointmentBaySelectionPolicy.BuildPolicy(services, packages, requestedCluster);
        }

        private async Task<long> ResolveAvailableBayIdForBookingAsync(
            long branchId,
            DateTime startAt,
            DateTime endAt,
            BaySelectionPolicy baySelectionPolicy)
        {
            var activeBays = await _store.GetActiveBaysAsync(branchId, ordered: true);

            if (activeBays.Count == 0)
            {
                throw new InvalidOperationException("Chi nhanh chua cau hinh khoang hoat dong.");
            }

            var blockingBayIds = (await _store.GetBlockingBayIdsAsync(branchId, startAt, endAt))
                .ToHashSet();

            foreach (var bay in AppointmentBaySelectionPolicy.OrderCandidateBays(
                         activeBays,
                         baySelectionPolicy.RequestedCluster,
                         baySelectionPolicy.IsDiagnoseOnly,
                         baySelectionPolicy.OrderedBayTypes))
            {
                if (!blockingBayIds.Contains(bay.Id))
                {
                    return bay.Id;
                }
            }

            throw new InvalidOperationException("Không tìm thấy khoang trống liên tiếp cho khung giờ đã chọn.");
        }

        private static int ResolveFlexibleIntakeCapacity(int windowMinutes)
        {
            var effectiveWindow = Math.Max(StandardCheckInSetupMinutes, windowMinutes);
            var slotsPerReceptionist = Math.Max(1, effectiveWindow / StandardCheckInSetupMinutes);
            return Math.Max(1, FlexibleReceptionistCount * slotsPerReceptionist);
        }

        private async Task<List<StandardService>> LoadRequestedServicesWithPackageItemsAsync(
            string? requestedServiceIds,
            string? requestedPackageIds)
        {
            var serviceIds = ParseIds(requestedServiceIds)
                .Concat(await _packageResolver.ResolvePackageItemServiceIdsAsync(requestedPackageIds))
                .Distinct()
                .ToList();

            if (serviceIds.Count == 0)
            {
                return new List<StandardService>();
            }

            return await _store.GetActiveStandardServicesByIdsAsync(serviceIds, includeCategory: true);
        }

        private async Task<List<CapacityConfig>> LoadCapacityConfigsForDayAsync(long branchId, DateOnly date)
        {
            var normalizedDayOfWeek = ToCapacityConfigDayOfWeek(date.DayOfWeek);
            return await _store.GetActiveCapacityConfigsForDayAsync(branchId, normalizedDayOfWeek);
        }
    }
}
