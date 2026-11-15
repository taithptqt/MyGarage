using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal sealed record CapacityProfile(int Quick, int Repair, int Total);
    internal sealed record SlotEvaluation(bool IsAvailable, int RemainingCapacity, int MaxCapacity, int QuickBooked, int HeavyBooked);
    internal sealed record PartDemand(long ProductId, decimal RequiredQuantity);

    internal static partial class AppointmentSchedulingHelpers
    {
        internal const int GraceArrivalMinutes = 15;
        internal const int NoShowMinutes = 30;
        internal const int StandardCheckInSetupMinutes = 15;
        internal const int ExecutionBufferMinutes = 15;
        internal const int FlexibleReceptionistCount = 2;
        internal const int FlexibleMaxBayMoves = 1;
        internal const int BookingLockHours = 4;
        internal const string DefaultDiagnosticServiceCode = "CDTC-001";
        internal const string FlexibleCheckInSlotMarker = WorkSlotNoteMarkers.FlexCheckIn;
        internal const string FlexiblePreallocatedRepairSlotMarker = WorkSlotNoteMarkers.FlexPreallocatedRepair;
        internal const string FlexiblePendingBayAssignmentMarker = WorkSlotNoteMarkers.FlexPendingBayAssignment;
        internal const string FlexiblePendingDispatchAlertMarker = WorkSlotNoteMarkers.DispatchAlertManualAssignmentRequired;

        private const decimal DefaultOnlineBookingThresholdPercent = 0.85m;
        private const decimal MinOnlineBookingThresholdPercent = 0.70m;
        private const decimal MaxOnlineBookingThresholdPercent = 0.95m;

        internal static CapacityProfile ComputeCapacity(List<Bay> bays)
        {
            var quick = bays.Count(IsQuickBay);
            var repair = bays.Count - quick;
            return new CapacityProfile(quick, repair, bays.Count);
        }

        internal static (int lockedQuick, int lockedRepair) CountLockedBays(
            IEnumerable<Bay> activeBays,
            IEnumerable<WorkSlot> locks,
            DateTime start,
            DateTime end)
        {
            var bayById = activeBays.ToDictionary(b => b.Id, b => b);
            var overlappingBayIds = locks
                .Where(l => l.BayId.HasValue && l.StartTime < end && l.EndTime > start && !IsBufferSlotType(l.SlotType))
                .Select(l => l.BayId!.Value)
                .Distinct()
                .ToList();
            var quick = 0;
            var repair = 0;

            foreach (var bayId in overlappingBayIds)
            {
                if (!bayById.TryGetValue(bayId, out var bay))
                {
                    continue;
                }

                if (IsQuickBay(bay))
                {
                    quick++;
                }
                else
                {
                    repair++;
                }
            }

            return (quick, repair);
        }

        internal static byte ToCapacityConfigDayOfWeek(DayOfWeek dayOfWeek)
        {
            var raw = (int)dayOfWeek;
            return (byte)(raw == 0 ? 7 : raw);
        }

        internal static int ResolveConfiguredCapacityLimit(
            DateTime startAt,
            DateTime endAt,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            int fallbackLimit,
            long branchId,
            int stepMinutes = 15)
        {
            if (capacityConfigs.Count == 0 || fallbackLimit <= 0 || endAt <= startAt)
            {
                return fallbackLimit;
            }

            var effectiveStep = stepMinutes > 0 ? stepMinutes : 15;
            var minLimit = int.MaxValue;
            for (var cursor = startAt; cursor < endAt; cursor = cursor.AddMinutes(effectiveStep))
            {
                var sliceLimit = ResolveCapacityAt(cursor, capacityConfigs, fallbackLimit, branchId);
                minLimit = Math.Min(minLimit, sliceLimit);
            }

            return minLimit == int.MaxValue ? fallbackLimit : minLimit;
        }

        private static int ResolveCapacityAt(
            DateTime at,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            int fallbackLimit,
            long branchId)
        {
            var match = ResolveCapacityConfigAt(at, capacityConfigs, branchId);
            if (match != null)
            {
                return Math.Max(0, match.MaxBaysAvailable);
            }

            return fallbackLimit;
        }

        internal static int ResolveConfiguredOnlineBookingLimit(
            DateTime startAt,
            DateTime endAt,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            int configuredCapacityLimit,
            long branchId,
            int stepMinutes = 15)
        {
            if (configuredCapacityLimit <= 0)
            {
                return 0;
            }

            if (endAt <= startAt)
            {
                return ComputeOnlineBookingLimit(
                    configuredCapacityLimit,
                    DefaultOnlineBookingThresholdPercent);
            }

            var effectiveStep = stepMinutes > 0 ? stepMinutes : 15;
            var minLimit = int.MaxValue;
            for (var cursor = startAt; cursor < endAt; cursor = cursor.AddMinutes(effectiveStep))
            {
                var sliceCapacity = ResolveCapacityAt(cursor, capacityConfigs, configuredCapacityLimit, branchId);
                var sliceThreshold = ResolveOnlineBookingThresholdAt(cursor, capacityConfigs, branchId);
                var sliceLimit = ComputeOnlineBookingLimit(sliceCapacity, sliceThreshold);
                minLimit = Math.Min(minLimit, sliceLimit);
            }

            if (minLimit == int.MaxValue)
            {
                minLimit = ComputeOnlineBookingLimit(
                    configuredCapacityLimit,
                    DefaultOnlineBookingThresholdPercent);
            }

            return Math.Max(0, Math.Min(configuredCapacityLimit, minLimit));
        }

        private static CapacityConfig? ResolveCapacityConfigAt(
            DateTime at,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            long branchId)
        {
            var time = TimeOnly.FromDateTime(at);
            var branchMatch = capacityConfigs.FirstOrDefault(c =>
                c.BranchId == branchId &&
                c.StartTime <= time &&
                c.EndTime > time);
            if (branchMatch != null)
            {
                return branchMatch;
            }

            return capacityConfigs.FirstOrDefault(c =>
                c.BranchId == null &&
                c.StartTime <= time &&
                c.EndTime > time);
        }

        private static decimal ResolveOnlineBookingThresholdAt(
            DateTime at,
            IReadOnlyList<CapacityConfig> capacityConfigs,
            long branchId)
        {
            var match = ResolveCapacityConfigAt(at, capacityConfigs, branchId);
            var configured = match?.BookingThresholdPercent;
            return NormalizeOnlineBookingThreshold(configured);
        }

        private static decimal NormalizeOnlineBookingThreshold(decimal? value)
        {
            var effective = value ?? DefaultOnlineBookingThresholdPercent;
            if (effective < MinOnlineBookingThresholdPercent)
            {
                return MinOnlineBookingThresholdPercent;
            }

            if (effective > MaxOnlineBookingThresholdPercent)
            {
                return MaxOnlineBookingThresholdPercent;
            }

            return effective;
        }

        private static int ComputeOnlineBookingLimit(int maxBays, decimal thresholdPercent)
        {
            if (maxBays <= 0)
            {
                return 0;
            }

            var normalizedThreshold = NormalizeOnlineBookingThreshold(thresholdPercent);
            var computed = (int)Math.Floor(maxBays * (double)normalizedThreshold);
            return Math.Max(1, computed);
        }

        internal static CapacityProfile ApplyConfiguredCapacityLimit(CapacityProfile current, int configuredLimit)
        {
            var cappedTotal = Math.Max(0, Math.Min(current.Total, configuredLimit));
            if (cappedTotal >= current.Total)
            {
                return current;
            }

            if (current.Total <= 0)
            {
                return new CapacityProfile(0, 0, 0);
            }

            var quickRatio = current.Quick / (double)current.Total;
            var quickCap = Math.Min(current.Quick, Math.Max(0, (int)Math.Floor(cappedTotal * quickRatio)));
            var repairCap = Math.Min(current.Repair, Math.Max(0, cappedTotal - quickCap));
            var remainder = cappedTotal - quickCap - repairCap;
            if (remainder > 0)
            {
                var addRepair = Math.Min(remainder, Math.Max(0, current.Repair - repairCap));
                repairCap += addRepair;
                remainder -= addRepair;
                if (remainder > 0)
                {
                    quickCap = Math.Min(current.Quick, quickCap + remainder);
                }
            }

            return new CapacityProfile(quickCap, repairCap, cappedTotal);
        }

        internal static SlotEvaluation EvaluateSlot(
            List<Appointment> overlap,
            ServiceCluster requestedCluster,
            CapacityProfile capacity,
            int onlineLimit)
        {
            if (onlineLimit <= 0 || capacity.Total <= 0)
            {
                return new SlotEvaluation(false, 0, 0, 0, 0);
            }

            int quickBooked = overlap.Count(a => ResolveFlowByRequestedServices(a.RequestedServiceIds, a.RequestedPackageIds, a.CustomerNotes) == ServiceCluster.Quick);
            int heavyBooked = overlap.Count - quickBooked;
            int totalBooked = overlap.Count;
            int remainingByOnlineCap = Math.Max(0, onlineLimit - totalBooked);
            var clusterCaps = ComputeClusterOnlineCaps(capacity, onlineLimit);

            int heavyMax = clusterCaps.repairCap;
            int heavyRemaining = Math.Min(Math.Max(0, heavyMax - heavyBooked), remainingByOnlineCap);

            int repairFree = Math.Max(0, clusterCaps.repairCap - heavyBooked);
            double repairFreeRatio = clusterCaps.repairCap > 0
                ? repairFree / (double)clusterCaps.repairCap
                : 0d;
            bool quickFullyBooked = quickBooked >= clusterCaps.quickCap;
            bool allowQuickOverflow = quickFullyBooked && repairFreeRatio > 0.5d;
            int quickOverflow = allowQuickOverflow ? repairFree : 0;
            int quickMax = clusterCaps.quickCap + quickOverflow;
            int quickRemaining = Math.Min(Math.Max(0, quickMax - quickBooked), remainingByOnlineCap);

            if (requestedCluster == ServiceCluster.Heavy)
            {
                return new SlotEvaluation(heavyRemaining > 0, heavyRemaining, heavyMax, quickBooked, heavyBooked);
            }

            return new SlotEvaluation(quickRemaining > 0, quickRemaining, quickMax, quickBooked, heavyBooked);
        }

        private static (int quickCap, int repairCap) ComputeClusterOnlineCaps(CapacityProfile capacity, int onlineLimit)
        {
            var quickCap = Math.Max(0, capacity.Quick);
            var repairCap = Math.Max(0, capacity.Repair);

            if (quickCap + repairCap > onlineLimit)
            {
                var excess = (quickCap + repairCap) - onlineLimit;
                if (repairCap >= excess)
                {
                    repairCap -= excess;
                }
                else
                {
                    var remain = excess - repairCap;
                    repairCap = 0;
                    quickCap = Math.Max(0, quickCap - remain);
                }
            }

            return (quickCap, repairCap);
        }

        internal static int ResolveFlexibleIntakeCapacity(int windowMinutes)
        {
            var effectiveWindow = Math.Max(StandardCheckInSetupMinutes, windowMinutes);
            var slotsPerReceptionist = Math.Max(1, effectiveWindow / StandardCheckInSetupMinutes);
            return Math.Max(1, FlexibleReceptionistCount * slotsPerReceptionist);
        }

        internal static bool IsHeavyHint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var key = NormalizeText(value);
            return key.Contains("dong co")
                || key.Contains("hop so")
                || key.Contains("gam")
                || key.Contains("he thong dien")
                || key.Contains("dieu hoa")
                || key.Contains("sua chua")
                || key.Contains("dai tu")
                || key.Contains("heavy")
                || key.Contains("repair");
        }

        internal static ServiceCluster ResolveFlowByRequestedServices(string? requestedServiceIds, string? requestedPackageIds, string? notes)
        {
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains(AppointmentNoteMarkers.ClusterHeavy, StringComparison.OrdinalIgnoreCase))
                return ServiceCluster.Heavy;
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains(AppointmentNoteMarkers.ClusterQuick, StringComparison.OrdinalIgnoreCase))
                return ServiceCluster.Quick;
            if (IsHeavyHint(notes)) return ServiceCluster.Heavy;

            return string.IsNullOrWhiteSpace(requestedServiceIds) && string.IsNullOrWhiteSpace(requestedPackageIds)
                ? ServiceCluster.Heavy
                : ServiceCluster.Quick;
        }

        internal static string AppendFlowMarker(string? notes, BookingFlowType flow)
        {
            var marker = flow == BookingFlowType.Flexible ? AppointmentNoteMarkers.FlowFlexible : AppointmentNoteMarkers.FlowFixed;
            if (string.IsNullOrWhiteSpace(notes)) return marker;
            if (notes.Contains(AppointmentNoteMarkers.FlowPrefix, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {marker}";
        }

        internal static string AppendClusterMarker(string? notes, ServiceCluster cluster)
        {
            var marker = cluster == ServiceCluster.Heavy ? AppointmentNoteMarkers.ClusterHeavy : AppointmentNoteMarkers.ClusterQuick;
            if (string.IsNullOrWhiteSpace(notes)) return marker;
            if (notes.Contains(AppointmentNoteMarkers.ClusterPrefix, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {marker}";
        }

        internal static string AppendNoteMarker(string? notes, string marker)
        {
            var tag = $"[{marker}]";
            if (string.IsNullOrWhiteSpace(notes)) return tag;
            if (notes.Contains(tag, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {tag}";
        }

        internal static string? NormalizeRequestedIdsForStorage(string? raw)
        {
            var ids = ParseIds(raw).Distinct().ToList();
            return ids.Count == 0
                ? null
                : string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        }

        internal const decimal InventoryQuantityTolerance = 0.0001m;

        internal static void MarkWorkSlotAsPartsPending(WorkSlot slot, string? detailMessage)
        {
            slot.Notes = AppendWorkSlotMarker(slot.Notes, WorkSlotNoteMarkers.PartsShortageAlert);
            var safeDetail = string.IsNullOrWhiteSpace(detailMessage)
                ? "Thiếu phụ tùng bắt buộc"
                : detailMessage.Replace("]", ")").Trim();
            slot.Notes = AppendWorkSlotMarker(slot.Notes, $"{WorkSlotNoteMarkers.PartsShortageDetailsPrefix}{safeDetail}]");
        }

        internal static string ResolvePartLabel(long productId, string? name, string? sku)
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

        internal static string AppendWorkSlotMarker(string? notes, string marker)
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
    }
}
