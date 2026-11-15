using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentPreallocationService : IAppointmentPreallocationService
    {
        private readonly IAppointmentStore _store;

        private const int FlexibleMaxBayMoves = AppointmentSchedulingHelpers.FlexibleMaxBayMoves;
        private const string FlexiblePreallocatedRepairSlotMarker = AppointmentSchedulingHelpers.FlexiblePreallocatedRepairSlotMarker;
        private const string FlexiblePendingBayAssignmentMarker = AppointmentSchedulingHelpers.FlexiblePendingBayAssignmentMarker;
        private const string FlexiblePendingDispatchAlertMarker = AppointmentSchedulingHelpers.FlexiblePendingDispatchAlertMarker;

        private sealed record DailyOperatingWindow(bool IsClosed, TimeOnly OpenTime, TimeOnly CloseTime);
        private sealed record PlannedSegment(DateTime StartTime, DateTime EndTime);
        private sealed record PlannedBaySegment(DateTime StartTime, DateTime EndTime, long BayId);

        public AppointmentPreallocationService(IAppointmentStore store)
        {
            _store = store;
        }

        public async Task<List<Appointment>> LoadProjectedRepairReservationsAsync(
            long branchId,
            DateTime startAt,
            DateTime endAt)
        {
            if (endAt <= startAt)
            {
                return new List<Appointment>();
            }

            return await _store.GetProjectedRepairReservationsAsync(
                branchId,
                startAt,
                endAt,
                FlexiblePreallocatedRepairSlotMarker);
        }

        public async Task<List<WorkSlot>> BuildFlexibleRepairPreallocatedSlotsAsync(
            Appointment appointment,
            WorkSlot checkInSlot,
            ServiceCluster requestedCluster,
            IReadOnlyList<string> orderedBayTypes,
            string targetBayAreaTag,
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
                requestedCluster,
                orderedBayTypes,
                FlexibleMaxBayMoves);

            if (baySegments.Count == 0)
            {
                baySegments = await TryAllocateBaysForSegmentsAsync(
                    appointment.BranchId,
                    segments,
                    requestedCluster,
                    orderedBayTypes,
                    maxAllowedMoves: null);
            }

            if (baySegments.Count == 0)
            {
                return BuildFallbackPendingDispatchSlots(
                    appointment,
                    checkInSlot,
                    targetBayAreaTag,
                    segments);
            }

            return BuildAssignedFlexibleRepairSlots(appointment, checkInSlot, baySegments);
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
                    throw new InvalidOperationException("Không thể lập lịch khoang cho luồng flex trong phạm vi 10 năm.");
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
            ServiceCluster requestedCluster,
            IReadOnlyList<string> orderedBayTypes,
            int? maxAllowedMoves)
        {
            if (segments.Count == 0)
            {
                return new List<PlannedBaySegment>();
            }

            var activeBays = await _store.GetActiveBaysAsync(branchId, ordered: true);

            var candidateBays = AppointmentBaySelectionPolicy.OrderCandidateBays(
                    activeBays,
                    requestedCluster,
                    isDiagnoseOnly: false,
                    orderedBayTypes)
                .ToList();
            if (candidateBays.Count == 0)
            {
                throw new InvalidOperationException("Không có khoang hoạt động để xếp lịch sửa chữa.");
            }

            var scheduleStart = segments.Min(s => s.StartTime);
            var scheduleEnd = segments.Max(s => s.EndTime);
            var blockingSlots = await _store.GetBlockingWorkSlotRangesAsync(branchId, scheduleStart, scheduleEnd);

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
                    throw new InvalidOperationException("Không tìm thấy khoang trống để pre-allocate lịch sửa chữa cho luồng flex.");
                }

                if (activeBayId.HasValue && activeBayId.Value != selectedBay.Value)
                {
                    movesUsed++;
                    if (maxAllowedMoves.HasValue && movesUsed > maxAllowedMoves.Value)
                    {
                        throw new InvalidOperationException("Không tìm thấy phương án pre-allocate khoang sửa chữa với tối đa 1 lần chuyển khoang.");
                    }
                }

                if (!IsSegmentAvailable(occupiedByBay, selectedBay.Value, segment))
                {
                    throw new InvalidOperationException("Không tìm thấy khoang trống để pre-allocate lịch sửa chữa cho luồng flex.");
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
            ServiceCluster requestedCluster,
            IReadOnlyList<string> orderedBayTypes,
            int? maxAllowedMoves)
        {
            try
            {
                return await AllocateBaysForSegmentsAsync(
                    branchId,
                    segments,
                    requestedCluster,
                    orderedBayTypes,
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

        private static List<WorkSlot> BuildAssignedFlexibleRepairSlots(
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

        private static List<WorkSlot> BuildFallbackPendingDispatchSlots(
            Appointment appointment,
            WorkSlot checkInSlot,
            string targetBayAreaTag,
            IReadOnlyList<PlannedSegment> rawSegments)
        {
            var markerNotes =
                $"[AUTO_BOOKING]{FlexiblePreallocatedRepairSlotMarker}{FlexiblePendingBayAssignmentMarker}" +
                $"{FlexiblePendingDispatchAlertMarker}[TARGET_BAY_AREA:{targetBayAreaTag}]";

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
            var opHour = await _store.GetBranchOperatingHourAsync(branchId, dayOfWeek);

            var isClosed = opHour?.IsDayOff == true;
            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);

            var exception = await _store.GetBranchExceptionAsync(branchId, date);

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
