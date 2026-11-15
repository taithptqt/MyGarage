using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Constants;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentPartReservationService : IAppointmentPartReservationService
    {
        private const decimal InventoryQuantityTolerance = 0.0001m;

        private sealed record PartDemand(long ProductId, decimal RequiredQuantity);

        private readonly IAppointmentStore _store;
        private readonly ISystemClock _clock;
        private readonly IAppointmentPackageResolver _packageResolver;

        public AppointmentPartReservationService(
            IAppointmentStore store,
            ISystemClock clock,
            IAppointmentPackageResolver packageResolver)
        {
            _store = store;
            _clock = clock;
            _packageResolver = packageResolver;
        }

        public async Task<PartAvailabilityEvaluation> EvaluatePartAvailabilityForBookingAsync(
            long branchId,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            var demands = await BuildMandatoryPartDemandsAsync(vehicleModelId, requestedServiceIds, requestedPackageIds);
            if (demands.Count == 0)
            {
                return new PartAvailabilityEvaluation(Array.Empty<PartShortage>());
            }

            var productIds = demands.Select(x => x.ProductId).Distinct().ToList();
            var availableByProduct = await _store.GetAvailableInventoryByProductAsync(branchId, productIds);

            var shortages = demands
                .Select(demand =>
                {
                    availableByProduct.TryGetValue(demand.ProductId, out var available);
                    return new
                    {
                        demand.ProductId,
                        demand.RequiredQuantity,
                        AvailableQuantity = Math.Max(0m, available)
                    };
                })
                .Where(x => x.AvailableQuantity + InventoryQuantityTolerance < x.RequiredQuantity)
                .ToList();

            if (shortages.Count == 0)
            {
                return new PartAvailabilityEvaluation(Array.Empty<PartShortage>());
            }

            var shortageProductIds = shortages.Select(x => x.ProductId).Distinct().ToList();
            var productMap = await _store.GetProductLabelsByIdsAsync(shortageProductIds);
            var productLabelById = productMap.ToDictionary(
                p => p.Key,
                p => ResolvePartLabel(p.Value.Id, p.Value.Name, p.Value.Sku));

            var details = shortages
                .Select(x =>
                {
                    var label = productLabelById.TryGetValue(x.ProductId, out var resolvedLabel)
                        ? resolvedLabel
                        : ResolvePartLabel(x.ProductId, null, null);
                    return new PartShortage(
                        x.ProductId,
                        x.RequiredQuantity,
                        x.AvailableQuantity,
                        label);
                })
                .ToList();

            return new PartAvailabilityEvaluation(details);
        }

        public async Task ApplyOrMarkPartsReservationAsync(
            Appointment appointment,
            WorkSlot partReservationSlot,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds,
            long? actorStaffId,
            PartAvailabilityEvaluation partAvailability)
        {
            if (partAvailability.HasShortage)
            {
                MarkWorkSlotAsPartsPending(partReservationSlot, partAvailability.Shortages);
                await _store.SaveChangesInTransactionAsync();
                return;
            }

            try
            {
                await ReservePartsForWorkSlotAsync(
                    appointment.BranchId,
                    partReservationSlot,
                    vehicleModelId,
                    requestedServiceIds,
                    requestedPackageIds,
                    actorStaffId);
            }
            catch (InvalidOperationException ex) when (IsInventoryShortageException(ex))
            {
                MarkWorkSlotAsPartsPending(partReservationSlot, ex.Message);
                await _store.SaveChangesInTransactionAsync();
            }
        }

        private async Task<IReadOnlyList<PartDemand>> BuildMandatoryPartDemandsAsync(
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            if (!vehicleModelId.HasValue || vehicleModelId.Value <= 0)
            {
                return Array.Empty<PartDemand>();
            }

            var serviceIds = ParseIds(requestedServiceIds)
                .Concat(await _packageResolver.ResolvePackageItemServiceIdsAsync(requestedPackageIds))
                .Distinct()
                .ToList();
            if (serviceIds.Count == 0)
            {
                return Array.Empty<PartDemand>();
            }

            var snapshots = await _store.GetMandatoryPartDemandsAsync(vehicleModelId.Value, serviceIds);
            return snapshots.Select(x => new PartDemand(x.ProductId, x.RequiredQuantity)).ToList();
        }

        private async Task ReservePartsForWorkSlotAsync(
            long branchId,
            WorkSlot slot,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds,
            long? actorStaffId)
        {
            var demands = await BuildMandatoryPartDemandsAsync(vehicleModelId, requestedServiceIds, requestedPackageIds);
            if (demands.Count == 0)
            {
                return;
            }

            var now = _clock.UtcNow;
            var expiry = slot.EndTime.AddDays(1);
            foreach (var demand in demands)
            {
                await ReserveInventoryQuantityAsync(branchId, demand.ProductId, demand.RequiredQuantity);
                _store.AddPartInventoryReservation(new PartInventoryReservation
                {
                    WorkSlotId = slot.Id,
                    ProductId = demand.ProductId,
                    ReservedQuantity = demand.RequiredQuantity,
                    Status = "RESERVED",
                    ExpiryTime = expiry,
                    CreateAt = now,
                    CreateBy = actorStaffId,
                    UpdateAt = now,
                    UpdateBy = actorStaffId
                });
            }

            await _store.SaveChangesInTransactionAsync();
        }

        private async Task ReserveInventoryQuantityAsync(long branchId, long productId, decimal requiredQuantity)
        {
            var remaining = requiredQuantity;
            var inventoryRows = await _store.GetBranchInventoriesForProductAsync(branchId, productId, onlyWithReserved: false);

            foreach (var row in inventoryRows)
            {
                if (remaining <= InventoryQuantityTolerance)
                {
                    break;
                }

                var currentReserved = row.ReservedQuantity ?? 0m;
                var available = row.Quantity - currentReserved;
                if (available <= InventoryQuantityTolerance)
                {
                    continue;
                }

                var delta = Math.Min(available, remaining);
                row.ReservedQuantity = currentReserved + delta;
                remaining -= delta;
            }

            if (remaining > InventoryQuantityTolerance)
            {
                throw new InvalidOperationException($"Không đủ tồn kho de giu phu tung Product#{productId}.");
            }
        }

        private static void MarkWorkSlotAsPartsPending(WorkSlot slot, IReadOnlyList<PartShortage> shortages)
        {
            slot.Notes = AppendWorkSlotMarker(slot.Notes, WorkSlotNoteMarkers.PartsShortageAlert);
            var detail = BuildPartsShortageSummary(shortages);
            slot.Notes = AppendWorkSlotMarker(slot.Notes, $"{WorkSlotNoteMarkers.PartsShortageDetailsPrefix}{detail}]");
        }

        private static void MarkWorkSlotAsPartsPending(WorkSlot slot, string? detailMessage)
        {
            slot.Notes = AppendWorkSlotMarker(slot.Notes, WorkSlotNoteMarkers.PartsShortageAlert);
            var safeDetail = string.IsNullOrWhiteSpace(detailMessage)
                ? "Thiếu phụ tùng bắt buộc"
                : detailMessage.Replace("]", ")").Trim();
            slot.Notes = AppendWorkSlotMarker(slot.Notes, $"{WorkSlotNoteMarkers.PartsShortageDetailsPrefix}{safeDetail}]");
        }

        private static string BuildPartsShortageSummary(IReadOnlyList<PartShortage> shortages)
        {
            if (shortages == null || shortages.Count == 0)
            {
                return "Thiếu phụ tùng bắt buộc";
            }

            return string.Join("; ",
                shortages.Select(item =>
                    $"{item.ProductName.Replace("]", ")")} (cần {item.RequiredQuantity:0.###}, còn {item.AvailableQuantity:0.###})"));
        }

        private static string ResolvePartLabel(long productId, string? name, string? sku)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(sku))
            {
                return sku.Trim();
            }

            return $"Sản phẩm #{productId}";
        }

        private static string AppendWorkSlotMarker(string? notes, string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                return notes ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                return marker;
            }

            if (notes.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return notes;
            }

            return $"{notes} {marker}";
        }

        private static bool IsInventoryShortageException(InvalidOperationException exception)
        {
            var message = exception?.Message ?? string.Empty;
            return message.Contains("Không đủ tồn kho", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("không đủ tồn kho", StringComparison.OrdinalIgnoreCase);
        }
    }
}
