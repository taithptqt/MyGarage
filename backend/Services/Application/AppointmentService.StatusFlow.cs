using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<Appointment?> ConfirmAsync(long id, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanConfirm(appointment);
            await EnsurePartsReadyForConfirmationAsync(appointment, actorStaffId);

            appointment.Status = nameof(AppointmentStatus.CONFIRMED);
            return await PersistStatusChangeAsync(id, appointment, actorStaffId);
        }

        public async Task<Appointment?> CancelAsync(long id, string? reason, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCancel(appointment);

            var effectiveReason = _appointmentStatusPolicy.BuildCancelReason(appointment, reason, _clock.LocalNow);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await ReleaseBookingReservationArtifactsAsync(
                    appointment.Id,
                    appointment.BranchId,
                    actorStaffId,
                    _clock.LocalNow);

                appointment.Status = nameof(AppointmentStatus.CANCELED);
                appointment.CancelReason = effectiveReason;
                var updated = await PersistStatusChangeAsync(id, appointment, actorStaffId);

                await transaction.CommitAsync();
                return updated;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Appointment?> CheckInAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanCheckIn(appointment);

            var arriveTime = arrivedAt ?? _clock.LocalNow;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await ApplyArrivalStatusAsync(appointment, arriveTime);
                var updated = await PersistStatusChangeAsync(id, appointment, actorStaffId);
                if (updated != null)
                {
                    await EnsureServiceOrderExistsForArrivedAppointmentAsync(updated, arriveTime, actorStaffId);
                }

                await transaction.CommitAsync();
                return updated;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null, long? actorUserId = null)
        {
            var state = await LoadStatusContextAsync(id, actorUserId);
            if (state == null) return null;
            var (appointment, actorStaffId) = state.Value;
            _appointmentStatusPolicy.EnsureCanArrive(appointment);

            var arriveTime = arrivedAt ?? _clock.LocalNow;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await ApplyArrivalStatusAsync(appointment, arriveTime);
                var updated = await PersistStatusChangeAsync(id, appointment, actorStaffId);
                if (updated != null)
                {
                    await EnsureServiceOrderExistsForArrivedAppointmentAsync(updated, arriveTime, actorStaffId);
                }

                await transaction.CommitAsync();
                return updated;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task ApplyArrivalStatusAsync(Appointment appointment, DateTime arriveTime)
        {
            var flow = ResolveFlowByAppointment(appointment);
            if (flow == BookingFlowType.Flexible)
            {
                ApplyFlexibleArrival(appointment, arriveTime);
                return;
            }

            var lifecyclePolicy = ResolveLifecyclePolicy(appointment.BranchId, arriveTime);
            var lane = ResolveLifecycleLane(appointment);
            var cancelAfterMinutes = ResolveCancelAfterMinutes(lifecyclePolicy, lane, appointment.StartTime);

            if (arriveTime >= appointment.StartTime.AddMinutes(cancelAfterMinutes))
            {
                appointment.Status = nameof(AppointmentStatus.NO_SHOW);
                appointment.CancelReason = AppointmentCancelReasons.AutoNoShowLateArrival;
                await IncrementNoShowCountAsync(appointment.CustomerId);
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

        private async Task EnsurePartsReadyForConfirmationAsync(Appointment appointment, long? actorStaffId)
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

            var activeSlots = await _context.WorkSlots
                .Where(slot =>
                    slot.AppointmentId == appointment.Id &&
                    slot.Status != nameof(WorkSlotStatus.CANCELLED) &&
                    slot.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    slot.Status != nameof(WorkSlotStatus.COMPLETED))
                .OrderBy(slot => slot.SequenceOrder)
                .ThenBy(slot => slot.StartTime)
                .ThenBy(slot => slot.Id)
                .ToListAsync();

            var slotIds = activeSlots.Select(slot => slot.Id).Distinct().ToList();
            var reservedByProduct = new Dictionary<long, decimal>();
            if (slotIds.Count > 0)
            {
                reservedByProduct = await _context.PartInventoryReservations
                    .AsNoTracking()
                    .Where(reservation =>
                        slotIds.Contains(reservation.WorkSlotId) &&
                        reservation.Status == "RESERVED")
                    .GroupBy(reservation => reservation.ProductId)
                    .Select(group => new
                    {
                        ProductId = group.Key,
                        ReservedQuantity = group.Sum(item => item.ReservedQuantity)
                    })
                    .ToDictionaryAsync(item => item.ProductId, item => item.ReservedQuantity);
            }

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
                    await _context.SaveChangesAsync();
                }
                return;
            }

            var shortageProductIds = shortages.Select(item => item.ProductId).Distinct().ToList();
            var productLabels = await _context.Products
                .AsNoTracking()
                .Where(product => shortageProductIds.Contains(product.Id))
                .Select(product => new { product.Id, product.Name, product.Sku })
                .ToDictionaryAsync(product => product.Id, product => ResolvePartLabel(product.Id, product.Name, product.Sku));

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
            await _context.SaveChangesAsync();

            throw new InvalidOperationException(
                "Không thể xác nhận lịch vì phụ tùng giữ chỗ hiện không đủ. Lễ tân vui lòng liên hệ khách để xác nhận thời gian xử lý.");
        }

        private async Task<long?> ResolveVehicleModelIdForConfirmationAsync(Appointment appointment)
        {
            if (appointment.VehicleId.HasValue && appointment.VehicleId.Value > 0)
            {
                return await _context.Vehicles
                    .AsNoTracking()
                    .Where(vehicle => vehicle.Id == appointment.VehicleId.Value && vehicle.IsDeleted != true)
                    .Select(vehicle => (long?)vehicle.ModelId)
                    .FirstOrDefaultAsync();
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
            if (repairSlot != null)
            {
                return repairSlot;
            }

            return ordered[0];
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
