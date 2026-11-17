using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public sealed record PartShortage(long ProductId, decimal RequiredQuantity, decimal AvailableQuantity, string ProductName);

    public sealed record PartAvailabilityEvaluation(IReadOnlyList<PartShortage> Shortages)
    {
        public bool HasShortage => Shortages.Count > 0;
    }

    public interface IAppointmentBaySelectionService
    {
        Task EnsureRequestedServicesCompatibleWithVehicleEngineAsync(
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds);

        Task<PartAvailabilityEvaluation> EvaluatePartAvailabilityForBookingAsync(
            long branchId,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds = null);

        Task EnsureFixedFlowCapacityAsync(
            long branchId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            ServiceCluster requestedCluster,
            string? excludeHoldToken = null,
            IReadOnlyCollection<string>? processedHoldTokens = null);

        Task EnsureFlexibleQueueCapacityAsync(
            long branchId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            string? excludeHoldToken = null,
            IReadOnlyCollection<string>? processedHoldTokens = null);

        Task ApplyBookingReservationArtifactsAsync(
            Appointment appointment,
            BookingFlowType flow,
            ServiceCluster requestedCluster,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds,
            long? actorStaffId,
            int plannedDurationMinutes,
            int slotStepMinutes,
            PartAvailabilityEvaluation partAvailability);
    }
}
