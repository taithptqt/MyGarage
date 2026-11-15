using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const decimal InventoryQuantityTolerance = 0.0001m;

        private sealed record PartDemand(long ProductId, decimal RequiredQuantity);
        private sealed record PartShortage(long ProductId, decimal RequiredQuantity, decimal AvailableQuantity, string ProductName);
        private sealed record PartAvailabilityEvaluation(IReadOnlyList<PartShortage> Shortages)
        {
            public bool HasShortage => Shortages.Count > 0;
        }

        private async Task<long?> ResolveVehicleModelIdForBookingAsync(long customerId, BookAppointmentRequestDto dto)
        {
            if (dto.VehicleModelId.HasValue && dto.VehicleModelId.Value > 0)
            {
                return dto.VehicleModelId.Value;
            }

            if (!dto.VehicleId.HasValue || dto.VehicleId.Value <= 0)
            {
                return null;
            }

            await ValidateSelectedVehicleOwnershipAsync(customerId, dto.VehicleId);
            return await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.Id == dto.VehicleId.Value && v.IsDeleted != true)
                .Select(v => (long?)v.ModelId)
                .FirstOrDefaultAsync();
        }

        private async Task<PartAvailabilityEvaluation> EvaluatePartAvailabilityForBookingAsync(
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
            var availableByProduct = await _context.Inventories
                .AsNoTracking()
                .Where(i =>
                    productIds.Contains(i.ProductId) &&
                    i.Warehouse.BranchId == branchId &&
                    i.Warehouse.IsDeleted != true &&
                    i.Warehouse.IsActive != false)
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AvailableQuantity = g.Sum(i => i.Quantity - (i.ReservedQuantity ?? 0m))
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.AvailableQuantity);

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
            var productLabelById = await _context.Products
                .AsNoTracking()
                .Where(p => shortageProductIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Sku })
                .ToDictionaryAsync(p => p.Id, p => ResolvePartLabel(p.Id, p.Name, p.Sku));

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

        private async Task<IReadOnlyList<PartDemand>> BuildMandatoryPartDemandsAsync(
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            if (!vehicleModelId.HasValue || vehicleModelId.Value <= 0)
            {
                return Array.Empty<PartDemand>();
            }

            var serviceIds = ParseServiceIds(requestedServiceIds)
                .Concat(await ResolvePackageItemServiceIdsAsync(requestedPackageIds))
                .Distinct()
                .ToList();
            if (serviceIds.Count == 0)
            {
                return Array.Empty<PartDemand>();
            }

            return await _context.ServiceBoms
                .AsNoTracking()
                .Where(x =>
                    x.VehicleModelId == vehicleModelId.Value &&
                    serviceIds.Contains(x.ServiceId) &&
                    x.IsMandatory)
                .GroupBy(x => x.ProductId)
                .Select(g => new PartDemand(
                    g.Key,
                    g.Sum(x => x.Quantity)))
                .ToListAsync();
        }

        private async Task ApplyBookingReservationArtifactsAsync(
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
            var primarySlot = new WorkSlot
            {
                BranchId = appointment.BranchId,
                BayId = null,
                TechnicianId = null,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                SlotType = isDiagnoseOnly
                    ? nameof(WorkSlotType.DIAGNOSE)
                    : (flow == BookingFlowType.Flexible
                        ? nameof(WorkSlotType.BUFFER)
                        : nameof(WorkSlotType.REPAIR)),
                CustomerId = appointment.CustomerId,
                VehicleId = appointment.VehicleId,
                AppointmentId = appointment.Id,
                ServiceOrderId = null,
                IsAdditional = false,
                SequenceOrder = 1,
                Status = nameof(WorkSlotStatus.RESERVED),
                LockedUntil = null,
                Notes = isDiagnoseOnly
                    ? "[AUTO_BOOKING][DIAGNOSE_ONLY]"
                    : flow == BookingFlowType.Flexible
                        ? $"[AUTO_BOOKING]{FlexibleCheckInSlotMarker}"
                        : "[AUTO_BOOKING]"
            };

            if (flow == BookingFlowType.Fixed || isDiagnoseOnly)
            {
                primarySlot.BayId = await ResolveAvailableBayIdForBookingAsync(
                    appointment.BranchId,
                    appointment.StartTime,
                    appointment.EndTime,
                    baySelectionPolicy);
                primarySlot.SequenceOrder = isDiagnoseOnly ? 1 : 2;
            }

            _context.WorkSlots.Add(primarySlot);
            await SaveChangesInTransactionAsync();

            WorkSlot partReservationSlot = primarySlot;
            if (flow == BookingFlowType.Flexible && !isDiagnoseOnly)
            {
                var checkInMinutes = Math.Max(
                    slotStepMinutes,
                    (int)Math.Ceiling((appointment.EndTime - appointment.StartTime).TotalMinutes));
                var repairMinutes = Math.Max(slotStepMinutes, plannedDurationMinutes - checkInMinutes);
                var preallocatedSlots = await BuildFlexibleRepairPreallocatedSlotsAsync(
                    appointment,
                    primarySlot,
                    baySelectionPolicy,
                    repairMinutes,
                    slotStepMinutes);

                if (preallocatedSlots.Count > 0)
                {
                    _context.WorkSlots.AddRange(preallocatedSlots);
                    await SaveChangesInTransactionAsync();
                    partReservationSlot = preallocatedSlots[0];
                }
            }

            if (partAvailability.HasShortage)
            {
                MarkWorkSlotAsPartsPending(partReservationSlot, partAvailability.Shortages);
                await SaveChangesInTransactionAsync();
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
                await SaveChangesInTransactionAsync();
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
                _context.PartInventoryReservations.Add(new PartInventoryReservation
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

            await SaveChangesInTransactionAsync();
        }

        private async Task ReserveInventoryQuantityAsync(long branchId, long productId, decimal requiredQuantity)
        {
            var remaining = requiredQuantity;
            var inventoryRows = await _context.Inventories
                .Where(i =>
                    i.ProductId == productId &&
                    i.Warehouse.BranchId == branchId &&
                    i.Warehouse.IsDeleted != true &&
                    i.Warehouse.IsActive != false)
                .OrderByDescending(i => i.Quantity - (i.ReservedQuantity ?? 0m))
                .ToListAsync();

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

        private async Task<long> ResolveAvailableBayIdForBookingAsync(
            long branchId,
            DateTime startAt,
            DateTime endAt,
            BaySelectionPolicy baySelectionPolicy)
        {
            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b =>
                    b.BranchId == branchId &&
                    b.IsDeleted != true &&
                    b.IsActive == true)
                .OrderBy(b => b.SequenceOrder)
                .ThenBy(b => b.Code)
                .ToListAsync();

            if (activeBays.Count == 0)
            {
                throw new InvalidOperationException("Chi nhanh chua cau hinh khoang hoat dong.");
            }

            var blockingBayIds = (await _context.WorkSlots
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
                    .ToListAsync())
                .ToHashSet();

            foreach (var bay in OrderCandidateBays(
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

        private static IEnumerable<Bay> OrderCandidateBays(
            IReadOnlyList<Bay> activeBays,
            ServiceCluster requestedCluster,
            bool isDiagnoseOnly,
            IReadOnlyList<string>? preferredBayTypes = null)
        {
            IEnumerable<Bay> fallback = isDiagnoseOnly
                ? activeBays.Where(IsDiagnoseBay)
                    .Concat(activeBays.Where(IsQuickBay))
                    .Concat(activeBays.Where(IsRepairBay))
                : requestedCluster == ServiceCluster.Quick
                    ? activeBays.Where(IsQuickBay).Concat(activeBays.Where(IsRepairBay))
                    : activeBays.Where(IsRepairBay).Concat(activeBays.Where(IsQuickBay));

            var normalizedPreferredTypes = (preferredBayTypes ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeToken)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedPreferredTypes.Count == 0)
            {
                return fallback.DistinctBy(b => b.Id);
            }

            var preferred = normalizedPreferredTypes
                .SelectMany(preferredType => activeBays
                    .Where(b => string.Equals(
                        NormalizeToken(b.BayType),
                        preferredType,
                        StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return preferred.Concat(fallback).DistinctBy(b => b.Id);
        }

        private static bool IsDiagnoseBay(Bay bay)
        {
            var bayType = NormalizeText(bay.BayType ?? string.Empty);
            if (bayType == "sp_diagnose" || bayType == "diagnose")
            {
                return true;
            }

            var area = NormalizeText(bay.ServiceArea ?? string.Empty);
            return area == "diagnose" || area == "diagnosis";
        }

        private async Task ReleaseBookingReservationArtifactsAsync(
            long appointmentId,
            long branchId,
            long? actorStaffId,
            DateTime releasedAt)
        {
            var workSlots = await _context.WorkSlots
                .Where(s =>
                    s.AppointmentId == appointmentId &&
                    s.Status != nameof(WorkSlotStatus.CANCELED) &&
                    s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    s.Status != nameof(WorkSlotStatus.COMPLETED))
                .ToListAsync();

            foreach (var slot in workSlots)
            {
                slot.Status = nameof(WorkSlotStatus.CANCELED);
                slot.ActualReleaseTime ??= releasedAt;
                if (slot.EndTime < releasedAt)
                {
                    slot.EndTime = releasedAt;
                }
            }

            var slotIds = workSlots.Select(s => s.Id).Distinct().ToList();
            if (slotIds.Count == 0)
            {
                if (workSlots.Count > 0)
                {
                    await SaveChangesInTransactionAsync();
                }

                return;
            }

            var reservations = await _context.PartInventoryReservations
                .Where(r =>
                    slotIds.Contains(r.WorkSlotId) &&
                    r.Status == "RESERVED")
                .ToListAsync();

            foreach (var group in reservations.GroupBy(r => r.ProductId))
            {
                var releaseQty = group.Sum(x => x.ReservedQuantity);
                await ReleaseInventoryQuantityAsync(branchId, group.Key, releaseQty);
            }

            var now = _clock.UtcNow;
            foreach (var reservation in reservations)
            {
                reservation.Status = "CANCELED";
                reservation.UpdateAt = now;
                reservation.UpdateBy = actorStaffId;
            }

            await SaveChangesInTransactionAsync();
        }

        private async Task ReleaseInventoryQuantityAsync(long branchId, long productId, decimal quantityToRelease)
        {
            var remaining = quantityToRelease;
            var inventoryRows = await _context.Inventories
                .Where(i =>
                    i.ProductId == productId &&
                    i.Warehouse.BranchId == branchId &&
                    i.Warehouse.IsDeleted != true &&
                    i.Warehouse.IsActive != false &&
                    (i.ReservedQuantity ?? 0m) > 0m)
                .OrderByDescending(i => i.ReservedQuantity ?? 0m)
                .ToListAsync();

            foreach (var row in inventoryRows)
            {
                if (remaining <= InventoryQuantityTolerance)
                {
                    break;
                }

                var reserved = row.ReservedQuantity ?? 0m;
                if (reserved <= InventoryQuantityTolerance)
                {
                    continue;
                }

                var delta = Math.Min(reserved, remaining);
                row.ReservedQuantity = reserved - delta;
                remaining -= delta;
            }
        }

        private async Task SyncWorkSlotsAfterArrivalAsync(
            Appointment appointment,
            long serviceOrderId,
            DateTime arrivedAt)
        {
            var workSlots = await _context.WorkSlots
                .Where(s =>
                    s.AppointmentId == appointment.Id &&
                    s.Status != nameof(WorkSlotStatus.CANCELED) &&
                    s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    s.Status != nameof(WorkSlotStatus.COMPLETED) &&
                    s.Status != nameof(WorkSlotStatus.OVERNIGHT_HOLD))
                .ToListAsync();

            if (workSlots.Count == 0)
            {
                return;
            }

            foreach (var slot in workSlots)
            {
                slot.ServiceOrderId = serviceOrderId;
                var isFlexiblePreallocatedRepair = !string.IsNullOrWhiteSpace(slot.Notes) &&
                    slot.Notes.Contains(FlexiblePreallocatedRepairSlotMarker, StringComparison.OrdinalIgnoreCase);

                if (!isFlexiblePreallocatedRepair)
                {
                    slot.StartTime = appointment.StartTime;
                    slot.EndTime = appointment.EndTime;
                }

                if (string.Equals(slot.Status, nameof(WorkSlotStatus.LOCKED), StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(slot.Status, nameof(WorkSlotStatus.RESERVED), StringComparison.OrdinalIgnoreCase)
                        && !isFlexiblePreallocatedRepair))
                {
                    slot.Status = nameof(WorkSlotStatus.ARRIVED);
                }

                if (!isFlexiblePreallocatedRepair)
                {
                    slot.ActualStartTime ??= arrivedAt;
                }
            }

            await SaveChangesInTransactionAsync();
        }

        private async Task SaveChangesInTransactionAsync()
        {
            await ExecuteInTransactionAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });
        }

        private async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await TryBeginTransactionAsync();
                await action();
                if (tx != null)
                {
                    await tx.CommitAsync();
                }
            });
        }

        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync();
        }
    }
}

