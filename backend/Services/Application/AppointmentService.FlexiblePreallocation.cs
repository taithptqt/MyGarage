using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private sealed record DailyOperatingWindow(bool IsClosed, TimeOnly OpenTime, TimeOnly CloseTime);
        private sealed record PlannedSegment(DateTime StartTime, DateTime EndTime);
        private sealed record PlannedBaySegment(DateTime StartTime, DateTime EndTime, long BayId);

        private async Task<List<WorkSlot>> BuildFlexibleRepairPreallocatedSlotsAsync(
            Appointment appointment,
            WorkSlot checkInSlot,
            BaySelectionPolicy baySelectionPolicy,
            int repairMinutes,
            int slotStepMinutes)
        {
            if (repairMinutes <= 0)
            {
                return new List<WorkSlot>();
            }

            var segments = await BuildWorkingSegmentsForDurationAsync(
                appointment.BranchId,
                appointment.EndTime,
                repairMinutes,
                slotStepMinutes);
            if (segments.Count == 0)
            {
                return new List<WorkSlot>();
            }

            var baySegments = await TryAllocateBaysForSegmentsAsync(
                appointment.BranchId,
                segments,
                baySelectionPolicy,
                FlexibleMaxBayMoves);

            if (baySegments.Count == 0)
            {
                // Fallback: van uu tien gan BayId that su; cho phep doi khoang nhieu hon
                // de tranh tra ve "het cho" khi van con khoang trong theo tung phan doan.
                baySegments = await TryAllocateBaysForSegmentsAsync(
                    appointment.BranchId,
                    segments,
                    baySelectionPolicy,
                    maxAllowedMoves: null);
            }

            if (baySegments.Count == 0)
            {
                return BuildFallbackPendingDispatchSlots(
                    appointment,
                    checkInSlot,
                    baySelectionPolicy.PrimaryArea,
                    segments);
            }

            return BuildAssignedFlexibleRepairSlots(appointment, checkInSlot, baySegments);
        }

        private async Task<List<Appointment>> LoadProjectedRepairReservationsAsync(
            long branchId,
            DateTime startAt,
            DateTime endAt)
        {
            if (endAt <= startAt)
            {
                return new List<Appointment>();
            }

            return await (
                    from slot in _context.WorkSlots.AsNoTracking()
                    join appointment in _context.Appointments.AsNoTracking()
                        on slot.AppointmentId equals appointment.Id
                    where slot.BranchId == branchId
                          && slot.StartTime < endAt
                          && slot.EndTime > startAt
                          && slot.SlotType == nameof(WorkSlotType.REPAIR)
                          && slot.Status != nameof(WorkSlotStatus.CANCELED)
                          && slot.Status != nameof(WorkSlotStatus.NOSHOW)
                          && slot.Status != nameof(WorkSlotStatus.COMPLETED)
                          && slot.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING)
                          && slot.Notes != null
                          && slot.Notes.Contains(FlexiblePreallocatedRepairSlotMarker)
                    select new Appointment
                    {
                        BranchId = slot.BranchId,
                        CustomerId = appointment.CustomerId,
                        VehicleId = appointment.VehicleId,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        RequestedServiceIds = appointment.RequestedServiceIds,
                        RequestedPackageIds = appointment.RequestedPackageIds,
                        CustomerNotes = appointment.CustomerNotes,
                        Status = appointment.Status
                    })
                .ToListAsync();
        }

        private async Task<List<PlannedSegment>> BuildWorkingSegmentsForDurationAsync(
            long branchId,
            DateTime startAt,
            int durationMinutes,
            int slotStepMinutes)
        {
            var effectiveStep = ResolveSlotStepMinutes(slotStepMinutes);
            var remainingMinutes = RoundUpToStep(durationMinutes, effectiveStep);
            var cursor = startAt;
            var segments = new List<PlannedSegment>();
            var cache = new Dictionary<DateOnly, DailyOperatingWindow>();
            var guard = 0;

            while (remainingMinutes > 0)
            {
                if (guard++ > 3650)
                {
                    throw new InvalidOperationException("Khong the lap lich khoang cho luong flex trong pham vi 10 nam.");
                }

                var date = DateOnly.FromDateTime(cursor);
                var dailyWindow = await ResolveDailyOperatingWindowAsync(branchId, date, cache);
                if (dailyWindow.IsClosed)
                {
                    cursor = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
                    continue;
                }

                var dayOpenAt = date.ToDateTime(dailyWindow.OpenTime);
                var dayCloseAt = date.ToDateTime(dailyWindow.CloseTime);
                if (cursor < dayOpenAt)
                {
                    cursor = dayOpenAt;
                }

                if (cursor >= dayCloseAt)
                {
                    cursor = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
                    continue;
                }

                var availableMinutes = (int)Math.Floor((dayCloseAt - cursor).TotalMinutes);
                if (availableMinutes <= 0)
                {
                    cursor = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
                    continue;
                }

                var consumableToday = Math.Min(remainingMinutes, availableMinutes);
                while (consumableToday > 0)
                {
                    var chunkMinutes = Math.Min(effectiveStep, consumableToday);
                    var endTime = cursor.AddMinutes(chunkMinutes);
                    segments.Add(new PlannedSegment(cursor, endTime));
                    cursor = endTime;
                    consumableToday -= chunkMinutes;
                    remainingMinutes -= chunkMinutes;
                }

                if (cursor >= dayCloseAt)
                {
                    cursor = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
                }
            }

            return segments;
        }

        private async Task<List<PlannedBaySegment>> AllocateBaysForSegmentsAsync(
            long branchId,
            IReadOnlyList<PlannedSegment> segments,
            BaySelectionPolicy baySelectionPolicy,
            int? maxAllowedMoves)
        {
            if (segments.Count == 0)
            {
                return new List<PlannedBaySegment>();
            }

            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b =>
                    b.BranchId == branchId &&
                    b.IsDeleted != true &&
                    b.IsActive == true)
                .OrderBy(b => b.SequenceOrder)
                .ThenBy(b => b.Code)
                .ToListAsync();

            var candidateBays = OrderCandidateBays(
                    activeBays,
                    baySelectionPolicy.RequestedCluster,
                    isDiagnoseOnly: false,
                    baySelectionPolicy.OrderedBayTypes)
                .ToList();
            if (candidateBays.Count == 0)
            {
                throw new InvalidOperationException("Khong co khoang hoat dong de xep lich sua chua.");
            }

            var scheduleStart = segments.Min(s => s.StartTime);
            var scheduleEnd = segments.Max(s => s.EndTime);
            var blockingSlots = await _context.WorkSlots
                .AsNoTracking()
                .Where(s =>
                    s.BranchId == branchId &&
                    s.BayId.HasValue &&
                    s.StartTime < scheduleEnd &&
                    s.EndTime > scheduleStart &&
                    s.Status != nameof(WorkSlotStatus.CANCELED) &&
                    s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    s.Status != nameof(WorkSlotStatus.COMPLETED) &&
                    s.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING))
                .Select(s => new { BayId = s.BayId!.Value, s.StartTime, s.EndTime })
                .ToListAsync();

            var occupiedByBay = candidateBays.ToDictionary(
                bay => bay.Id,
                _ => new List<PlannedSegment>());
            foreach (var existing in blockingSlots)
            {
                if (!occupiedByBay.TryGetValue(existing.BayId, out var list))
                {
                    continue;
                }

                list.Add(new PlannedSegment(existing.StartTime, existing.EndTime));
            }

            var orderedSegments = segments
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.EndTime)
                .ToList();

            var assigned = new List<PlannedBaySegment>(orderedSegments.Count);
            long? activeBayId = null;
            var movesUsed = 0;

            for (var index = 0; index < orderedSegments.Count; index++)
            {
                var segment = orderedSegments[index];
                if (activeBayId.HasValue && IsSegmentAvailable(occupiedByBay, activeBayId.Value, segment))
                {
                    occupiedByBay[activeBayId.Value].Add(segment);
                    assigned.Add(new PlannedBaySegment(segment.StartTime, segment.EndTime, activeBayId.Value));
                    continue;
                }

                var selectedBay = SelectBestCandidateBayForSegmentRange(
                    candidateBays,
                    occupiedByBay,
                    orderedSegments,
                    index,
                    activeBayId);

                if (!selectedBay.HasValue)
                {
                    throw new InvalidOperationException("Khong tim thay khoang trong de pre-allocate lich sua chua cho luong flex.");
                }

                if (activeBayId.HasValue && activeBayId.Value != selectedBay.Value)
                {
                    movesUsed++;
                    if (maxAllowedMoves.HasValue && movesUsed > maxAllowedMoves.Value)
                    {
                        throw new InvalidOperationException("Khong tim thay phuong an pre-allocate khoang sua chua voi toi da 1 lan chuyen khoang.");
                    }
                }

                if (!IsSegmentAvailable(occupiedByBay, selectedBay.Value, segment))
                {
                    throw new InvalidOperationException("Khong tim thay khoang trong de pre-allocate lich sua chua cho luong flex.");
                }

                activeBayId = selectedBay.Value;
                occupiedByBay[activeBayId.Value].Add(segment);
                assigned.Add(new PlannedBaySegment(segment.StartTime, segment.EndTime, activeBayId.Value));
            }

            return CollapseAdjacentBaySegments(assigned);
        }

        private async Task<List<PlannedBaySegment>> TryAllocateBaysForSegmentsAsync(
            long branchId,
            IReadOnlyList<PlannedSegment> segments,
            BaySelectionPolicy baySelectionPolicy,
            int? maxAllowedMoves)
        {
            try
            {
                return await AllocateBaysForSegmentsAsync(
                    branchId,
                    segments,
                    baySelectionPolicy,
                    maxAllowedMoves);
            }
            catch (InvalidOperationException)
            {
                return new List<PlannedBaySegment>();
            }
        }

        private static bool IsSegmentAvailable(
            IReadOnlyDictionary<long, List<PlannedSegment>> occupiedByBay,
            long bayId,
            PlannedSegment segment)
        {
            if (!occupiedByBay.TryGetValue(bayId, out var intervals))
            {
                return false;
            }

            return !intervals.Any(i => i.StartTime < segment.EndTime && i.EndTime > segment.StartTime);
        }

        private static long? SelectBestCandidateBayForSegmentRange(
            IReadOnlyList<Bay> candidateBays,
            IReadOnlyDictionary<long, List<PlannedSegment>> occupiedByBay,
            IReadOnlyList<PlannedSegment> orderedSegments,
            int startIndex,
            long? preferredBayId)
        {
            var candidates = candidateBays.Select(b => b.Id).ToList();
            if (preferredBayId.HasValue && candidates.Contains(preferredBayId.Value))
            {
                candidates.Remove(preferredBayId.Value);
                candidates.Insert(0, preferredBayId.Value);
            }

            long? bestBayId = null;
            var bestScore = -1;
            foreach (var bayId in candidates)
            {
                var score = CountContinuousAssignableSegments(occupiedByBay, bayId, orderedSegments, startIndex);
                if (score <= 0)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBayId = bayId;
                }
            }

            return bestBayId;
        }

        private static int CountContinuousAssignableSegments(
            IReadOnlyDictionary<long, List<PlannedSegment>> occupiedByBay,
            long bayId,
            IReadOnlyList<PlannedSegment> orderedSegments,
            int startIndex)
        {
            var count = 0;
            for (var i = startIndex; i < orderedSegments.Count; i++)
            {
                if (!IsSegmentAvailable(occupiedByBay, bayId, orderedSegments[i]))
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static List<PlannedBaySegment> CollapseAdjacentBaySegments(
            IReadOnlyList<PlannedBaySegment> segments)
        {
            if (segments.Count == 0)
            {
                return new List<PlannedBaySegment>();
            }

            var ordered = segments
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.EndTime)
                .ToList();
            var collapsed = new List<PlannedBaySegment>();
            var current = ordered[0];
            for (var i = 1; i < ordered.Count; i++)
            {
                var next = ordered[i];
                if (current.BayId == next.BayId && current.EndTime == next.StartTime)
                {
                    current = current with { EndTime = next.EndTime };
                    continue;
                }

                collapsed.Add(current);
                current = next;
            }

            collapsed.Add(current);
            return collapsed;
        }

        private static List<PlannedSegment> CollapseAdjacentSegments(
            IReadOnlyList<PlannedSegment> segments)
        {
            if (segments.Count == 0)
            {
                return new List<PlannedSegment>();
            }

            var ordered = segments
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.EndTime)
                .ToList();

            var collapsed = new List<PlannedSegment>();
            var current = ordered[0];
            for (var i = 1; i < ordered.Count; i++)
            {
                var next = ordered[i];
                if (current.EndTime == next.StartTime)
                {
                    current = current with { EndTime = next.EndTime };
                    continue;
                }

                collapsed.Add(current);
                current = next;
            }

            collapsed.Add(current);
            return collapsed;
        }

        private List<WorkSlot> BuildAssignedFlexibleRepairSlots(
            Appointment appointment,
            WorkSlot checkInSlot,
            IReadOnlyList<PlannedBaySegment> baySegments)
        {
            var slots = new List<WorkSlot>(baySegments.Count);
            var sequenceOrder = 2;
            foreach (var segment in baySegments)
            {
                slots.Add(new WorkSlot
                {
                    ParentSlotId = checkInSlot.Id,
                    BranchId = appointment.BranchId,
                    BayId = segment.BayId,
                    TechnicianId = null,
                    StartTime = segment.StartTime,
                    EndTime = segment.EndTime,
                    SlotType = nameof(WorkSlotType.REPAIR),
                    CustomerId = appointment.CustomerId,
                    VehicleId = appointment.VehicleId,
                    AppointmentId = appointment.Id,
                    ServiceOrderId = null,
                    IsAdditional = false,
                    SequenceOrder = sequenceOrder++,
                    Status = nameof(WorkSlotStatus.RESERVED),
                    LockedUntil = null,
                    Notes = $"[AUTO_BOOKING]{FlexiblePreallocatedRepairSlotMarker}"
                });
            }

            return slots;
        }

        private List<WorkSlot> BuildFallbackPendingDispatchSlots(
            Appointment appointment,
            WorkSlot checkInSlot,
            BookingBayArea primaryArea,
            IReadOnlyList<PlannedSegment> rawSegments)
        {
            var targetBayArea = ResolveTargetBayAreaTag(primaryArea);
            var markerNotes =
                $"[AUTO_BOOKING]{FlexiblePreallocatedRepairSlotMarker}{FlexiblePendingBayAssignmentMarker}" +
                $"{FlexiblePendingDispatchAlertMarker}[TARGET_BAY_AREA:{targetBayArea}]";

            var collapsedSegments = CollapseAdjacentSegments(rawSegments);
            var slots = new List<WorkSlot>(collapsedSegments.Count);
            var sequenceOrder = 2;
            foreach (var segment in collapsedSegments)
            {
                slots.Add(new WorkSlot
                {
                    ParentSlotId = checkInSlot.Id,
                    BranchId = appointment.BranchId,
                    BayId = null,
                    TechnicianId = null,
                    StartTime = segment.StartTime,
                    EndTime = segment.EndTime,
                    SlotType = nameof(WorkSlotType.REPAIR),
                    CustomerId = appointment.CustomerId,
                    VehicleId = appointment.VehicleId,
                    AppointmentId = appointment.Id,
                    ServiceOrderId = null,
                    IsAdditional = false,
                    SequenceOrder = sequenceOrder++,
                    Status = nameof(WorkSlotStatus.RESERVED),
                    LockedUntil = null,
                    Notes = markerNotes
                });
            }

            return slots;
        }

        private async Task<DailyOperatingWindow> ResolveDailyOperatingWindowAsync(
            long branchId,
            DateOnly date,
            Dictionary<DateOnly, DailyOperatingWindow> cache)
        {
            if (cache.TryGetValue(date, out var cached))
            {
                return cached;
            }

            var dayOfWeek = (int)date.DayOfWeek;
            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == dayOfWeek);

            var isClosed = opHour?.IsDayOff == true;
            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);

            var exception = await _context.BranchExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.BranchId == branchId && e.ExceptionDate == date);

            if (exception?.IsClosed == true)
            {
                isClosed = true;
            }

            if (exception?.StartTime.HasValue == true)
            {
                openTime = exception.StartTime.Value;
            }

            if (exception?.EndTime.HasValue == true)
            {
                closeTime = exception.EndTime.Value;
            }

            if (closeTime <= openTime)
            {
                isClosed = true;
            }

            var resolved = new DailyOperatingWindow(isClosed, openTime, closeTime);
            cache[date] = resolved;
            return resolved;
        }
    }
}

