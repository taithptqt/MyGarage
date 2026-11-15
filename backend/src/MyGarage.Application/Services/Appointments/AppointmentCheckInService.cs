using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentCheckInService : IAppointmentCheckInService
    {
        private readonly IAppointmentStore _store;
        private readonly IAppointmentSchedulingAvailability _schedulingAvailability;
        private readonly IAppointmentPackageResolver _packageResolver;
        private readonly IAppointmentNoShowService _noShowService;
        private readonly IAppointmentCheckInOrderService _checkInOrderService;
        private readonly ISystemClock _clock;

        public AppointmentCheckInService(
            IAppointmentStore store,
            IAppointmentSchedulingAvailability schedulingAvailability,
            IAppointmentPackageResolver packageResolver,
            IAppointmentNoShowService noShowService,
            IAppointmentCheckInOrderService checkInOrderService,
            ISystemClock clock)
        {
            _store = store;
            _schedulingAvailability = schedulingAvailability;
            _packageResolver = packageResolver;
            _noShowService = noShowService;
            _checkInOrderService = checkInOrderService;
            _clock = clock;
        }

        public Task EnsureServiceOrderExistsForArrivedAppointmentAsync(
            Appointment appointment,
            DateTime arriveTime,
            long? actorStaffId)
            => _checkInOrderService.EnsureServiceOrderExistsForArrivedAppointmentAsync(
                appointment,
                arriveTime,
                actorStaffId);

        public async Task ApplyArrivalStatusAsync(Appointment appointment, DateTime arriveTime)
        {
            var flow = _schedulingAvailability.ResolveFlowByAppointment(appointment);
            if (flow == BookingFlowType.Flexible)
            {
                ApplyFlexibleArrival(appointment, arriveTime);
                return;
            }

            var cancelAfterMinutes = _noShowService.ResolveCancelAfterMinutes(appointment, arriveTime);

            if (arriveTime >= appointment.StartTime.AddMinutes(cancelAfterMinutes))
            {
                appointment.Status = nameof(AppointmentStatus.NO_SHOW);
                appointment.CancelReason = AppointmentCancelReasons.AutoNoShowLateArrival;
                await _noShowService.IncrementNoShowCountAsync(appointment.CustomerId);
                return;
            }

            var arrivalOffsetMinutes = (arriveTime - appointment.StartTime).TotalMinutes;

            if (arrivalOffsetMinutes < -GraceArrivalMinutes)
            {
                await ApplyEarlyFixedArrivalAsync(appointment, arriveTime);
                return;
            }

            if (arrivalOffsetMinutes > GraceArrivalMinutes)
            {
                await ApplyLateFixedArrivalAsync(appointment, arriveTime, cancelAfterMinutes);
                return;
            }

            appointment.Status = nameof(AppointmentStatus.ARRIVED);
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "ON_TIME_ARRIVAL");
        }

        public async Task EnsurePartsReadyForConfirmationAsync(Appointment appointment, long? actorStaffId)
        {
            if (appointment == null)
            {
                throw new InvalidOperationException("Không tìm thấy lịch hẹn để xác nhận.");
            }

            var vehicleModelId = await ResolveVehicleModelIdForConfirmationAsync(appointment);
            var demands = await BuildMandatoryPartDemandsAsync(vehicleModelId, appointment.RequestedServiceIds, appointment.RequestedPackageIds);
            if (demands.Count == 0)
            {
                return;
            }

            var activeSlots = await _store.GetActiveWorkSlotsByAppointmentAsync(appointment.Id);

            var slotIds = activeSlots.Select(slot => slot.Id).Distinct().ToList();
            var reservedByProduct = await _store.GetReservedPartQuantitiesForSlotsAsync(slotIds);

            var shortages = demands
                .Select(demand =>
                {
                    reservedByProduct.TryGetValue(demand.ProductId, out var reservedQuantity);
                    return new
                    {
                        demand.ProductId,
                        demand.RequiredQuantity,
                        ReservedQuantity = Math.Max(0m, reservedQuantity)
                    };
                })
                .Where(item => item.ReservedQuantity + InventoryQuantityTolerance < item.RequiredQuantity)
                .ToList();

            if (shortages.Count == 0)
            {
                var cleared = ClearPartsFollowUpMarkers(activeSlots);
                if (cleared)
                {
                    ApplyUpdateAudit(appointment, actorStaffId);
                    await _store.SaveChangesInTransactionAsync();
                }
                return;
            }

            var shortageProductIds = shortages.Select(item => item.ProductId).Distinct().ToList();
            var productMap = await _store.GetProductLabelsByIdsAsync(shortageProductIds);
            var productLabels = productMap.ToDictionary(
                kvp => kvp.Key,
                kvp => ResolvePartLabel(kvp.Value.Id, kvp.Value.Name, kvp.Value.Sku));

            var details = shortages.Select(item =>
            {
                var label = productLabels.TryGetValue(item.ProductId, out var productLabel)
                    ? productLabel
                    : ResolvePartLabel(item.ProductId, null, null);
                return $"{label} (cần {item.RequiredQuantity:0.###}, đã giữ {item.ReservedQuantity:0.###})";
            }).ToList();

            var targetSlot = SelectPartAnchorSlot(activeSlots);
            if (targetSlot != null)
            {
                MarkWorkSlotAsPartsPending(targetSlot, string.Join("; ", details));
            }

            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "PARTS_CONFIRMATION_BLOCKED");
            ApplyUpdateAudit(appointment, actorStaffId);
            await _store.SaveChangesInTransactionAsync();

            throw new InvalidOperationException(
                "Không thể xác nhận lịch vì phụ tùng giữ chỗ hiện không đủ. Lễ tân vui lòng liên hệ khách để xác nhận thời gian xử lý.");
        }

        private void ApplyUpdateAudit(Appointment appointment, long? actorStaffId)
        {
            appointment.UpdatedAt = _clock.UtcNow;
            appointment.UpdatedBy = actorStaffId;
        }

        private static void ApplyFlexibleArrival(Appointment appointment, DateTime arriveTime)
        {
            if (arriveTime >= appointment.EndTime.AddMinutes(NoShowMinutes))
            {
                appointment.Status = nameof(AppointmentStatus.PENDING);
                appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "WINDOW_MISSED_REPRIORITIZED");
                return;
            }

            appointment.Status = nameof(AppointmentStatus.ARRIVED);
        }

        private async Task ApplyEarlyFixedArrivalAsync(Appointment appointment, DateTime arriveTime)
        {
            var candidates = (await _schedulingAvailability.GetAvailableSlotsAsync(
                    appointment.BranchId,
                    DateOnly.FromDateTime(appointment.StartTime),
                    appointment.RequestedServiceIds,
                    appointment.RequestedPackageIds,
                    AppointmentBookingFlows.Fixed))
                .Where(s => s.StartTime >= arriveTime && s.StartTime < appointment.StartTime)
                .OrderByDescending(s => s.IsTopPick)
                .ThenByDescending(s => s.CompactionScore)
                .ThenBy(s => s.StartTime)
                .ToList();

            if (candidates.Count == 0)
            {
                appointment.Status = nameof(AppointmentStatus.ARRIVED);
                appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "EARLY_WAIT_BOOKED_SLOT");
                return;
            }

            var preferred = candidates[0];
            appointment.StartTime = preferred.StartTime;
            appointment.EndTime = preferred.EndTime;
            appointment.Status = nameof(AppointmentStatus.ARRIVED);
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "EARLY_BROUGHT_FORWARD");
        }

        private async Task ApplyLateFixedArrivalAsync(Appointment appointment, DateTime arriveTime, int cancelAfterMinutes)
        {
            var lateMinutes = (arriveTime - appointment.StartTime).TotalMinutes;
            if (lateMinutes >= cancelAfterMinutes)
            {
                ApplyLateSwitchToFlexibleQueue(appointment);
                return;
            }

            var candidates = (await _schedulingAvailability.GetAvailableSlotsAsync(
                    appointment.BranchId,
                    DateOnly.FromDateTime(appointment.StartTime),
                    appointment.RequestedServiceIds,
                    appointment.RequestedPackageIds,
                    AppointmentBookingFlows.Fixed))
                .Where(s => s.StartTime >= arriveTime)
                .OrderByDescending(s => s.IsTopPick)
                .ThenByDescending(s => s.CompactionScore)
                .ThenBy(s => s.StartTime)
                .ToList();

            if (candidates.Count == 0)
            {
                ApplyLateSwitchToFlexibleQueue(appointment);
                return;
            }

            var next = candidates[0];
            appointment.StartTime = next.StartTime;
            appointment.EndTime = next.EndTime;
            appointment.Status = nameof(AppointmentStatus.ARRIVED_LATE);
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_RESCHEDULED");
        }

        private static void ApplyLateSwitchToFlexibleQueue(Appointment appointment)
        {
            appointment.Status = nameof(AppointmentStatus.PENDING);
            appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_SWITCHED_TO_FLEX_QUEUE");
        }

        private async Task<IReadOnlyList<PartDemand>> BuildMandatoryPartDemandsAsync(
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds)
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

        private async Task<long?> ResolveVehicleModelIdForConfirmationAsync(Appointment appointment)
        {
            if (appointment.VehicleId.HasValue && appointment.VehicleId.Value > 0)
            {
                return await _store.GetVehicleModelIdAsync(appointment.VehicleId.Value, requireNotDeleted: true);
            }

            return null;
        }

        private static WorkSlot? SelectPartAnchorSlot(IEnumerable<WorkSlot> slots)
        {
            var ordered = slots?.ToList() ?? new List<WorkSlot>();
            if (ordered.Count == 0)
            {
                return null;
            }

            var repairSlot = ordered.FirstOrDefault(slot => string.Equals(slot.SlotType, nameof(WorkSlotType.REPAIR), StringComparison.OrdinalIgnoreCase));
            return repairSlot ?? ordered[0];
        }

        private static bool ClearPartsFollowUpMarkers(IEnumerable<WorkSlot> slots)
        {
            var changed = false;
            foreach (var slot in slots ?? Array.Empty<WorkSlot>())
            {
                var cleanedNotes = RemovePartsShortageMarkers(slot.Notes);
                if (!string.Equals(cleanedNotes, slot.Notes, StringComparison.Ordinal))
                {
                    slot.Notes = cleanedNotes;
                    changed = true;
                }
            }

            return changed;
        }

        private static string? RemovePartsShortageMarkers(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return notes;
            }

            var output = notes.Replace(WorkSlotNoteMarkers.PartsShortageAlert, string.Empty, StringComparison.OrdinalIgnoreCase);
            var searchStart = 0;
            while (true)
            {
                var markerIndex = output.IndexOf(WorkSlotNoteMarkers.PartsShortageDetailsPrefix, searchStart, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    break;
                }

                var markerEnd = output.IndexOf(']', markerIndex);
                if (markerEnd < 0)
                {
                    output = output.Remove(markerIndex).Trim();
                    break;
                }

                output = output.Remove(markerIndex, markerEnd - markerIndex + 1);
                searchStart = markerIndex;
            }

            return NormalizeInlineSpacing(output);
        }

        private static string NormalizeInlineSpacing(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var segments = value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();

            return segments.Count == 0 ? string.Empty : string.Join(" ", segments);
        }
    }
}
