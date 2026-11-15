using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static partial class AppointmentSchedulingHelpers
    {
        internal static string NormalizeText(string value)
        {
            var lowered = value.Trim().ToLowerInvariant();
            return string.Concat(lowered.Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
                .Normalize(NormalizationForm.FormC);
        }

        internal static IEnumerable<long> ParseIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Enumerable.Empty<long>();
            return csv.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0);
        }

        internal static int RoundUpToStep(int minutes, int stepMinutes)
        {
            var step = stepMinutes > 0 ? stepMinutes : 15;
            if (minutes <= 0) return step;
            var mod = minutes % step;
            return mod == 0 ? minutes : minutes + (step - mod);
        }

        internal static int ResolveSlotStepMinutes(int? slotDurationMinutes)
        {
            var step = slotDurationMinutes.GetValueOrDefault(15);
            return step > 0 ? step : 15;
        }

        internal static bool IsQuickBay(Bay bay)
        {
            if (bay == null) return false;
            var area = NormalizeText(bay.ServiceArea ?? string.Empty);
            if (IsQuickArea(area))
            {
                return true;
            }

            var bayType = NormalizeText(bay.BayType ?? string.Empty);
            return IsQuickBayType(bayType);
        }

        internal static bool IsQuickSlotType(string? slotType, string? notes)
        {
            if (IsBufferSlotType(slotType))
            {
                return false;
            }

            if (string.Equals(slotType, nameof(WorkSlotType.DIAGNOSE), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var key = NormalizeText($"{slotType} {notes}");
            return key.Contains("quick") || key.Contains("maintenance") || key.Contains("bao duong") || key.Contains("dich vu nhanh");
        }

        internal static bool IsBufferSlotType(string? slotType)
        {
            return string.Equals(slotType, nameof(WorkSlotType.BUFFER), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRepairBay(Bay bay)
        {
            if (bay == null) return false;
            var area = NormalizeText(bay.ServiceArea ?? string.Empty);
            if (IsRepairArea(area))
            {
                return true;
            }

            var bayType = NormalizeText(bay.BayType ?? string.Empty);
            return IsRepairBayType(bayType);
        }

        internal static string BuildOvernightSourceMarker(long sourceSlotId) => $"[OVERNIGHT_FROM:{sourceSlotId}]";

        internal static WorkSlot BuildOvernightLockSlot(WorkSlot source, DateTime nextMorning, int lockMinutes)
        {
            return new WorkSlot
            {
                ParentSlotId = source.Id,
                BranchId = source.BranchId,
                BayId = source.BayId,
                TechnicianId = source.TechnicianId,
                StartTime = nextMorning,
                EndTime = nextMorning.AddMinutes(lockMinutes),
                SlotType = nameof(WorkSlotType.REPAIR),
                CustomerId = source.CustomerId,
                VehicleId = source.VehicleId,
                AppointmentId = source.AppointmentId,
                ServiceOrderId = source.ServiceOrderId,
                IsAdditional = source.IsAdditional,
                SequenceOrder = source.SequenceOrder > 0 ? source.SequenceOrder : 3,
                Status = nameof(WorkSlotStatus.OVERNIGHT_HOLD),
                Notes = BuildOvernightSourceMarker(source.Id)
            };
        }

        private static bool IsQuickArea(string normalizedServiceArea)
        {
            if (string.IsNullOrWhiteSpace(normalizedServiceArea)) return false;
            return normalizedServiceArea == "quick"
                || normalizedServiceArea == "qs"
                || normalizedServiceArea == "quick_service";
        }

        private static bool IsRepairArea(string normalizedServiceArea)
        {
            if (string.IsNullOrWhiteSpace(normalizedServiceArea)) return false;
            return normalizedServiceArea == "repair"
                || normalizedServiceArea == "gr"
                || normalizedServiceArea == "general_repair";
        }

        private static bool IsQuickBayType(string normalizedBayType)
        {
            if (string.IsNullOrWhiteSpace(normalizedBayType)) return false;
            return normalizedBayType is "qs_lift" or "qs_flat"
                or "quick_lift" or "quick_flat";
        }

        private static bool IsRepairBayType(string normalizedBayType)
        {
            if (string.IsNullOrWhiteSpace(normalizedBayType)) return false;
            return normalizedBayType is "gr_lift" or "gr_align" or "gr_electrical"
                or "general_lift" or "undercarriage" or "electrical";
        }

        internal static DateTime ResolveEffectiveSlotEnd(WorkSlot slot)
        {
            return slot.ActualReleaseTime ?? slot.EndTime;
        }

        internal static bool IsClosedWorkSlotStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return string.Equals(status, nameof(WorkSlotStatus.CANCELED), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, nameof(WorkSlotStatus.COMPLETED), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, nameof(WorkSlotStatus.NOSHOW), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "NO_SHOW", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldFallbackToFlexibleFlowForLongDuration(
            int plannedDurationMinutes,
            TimeOnly openTime,
            TimeOnly closeTime)
        {
            var openMinutes = openTime.Hour * 60 + openTime.Minute;
            var closeMinutes = closeTime.Hour * 60 + closeTime.Minute;
            var workingMinutes = Math.Max(0, closeMinutes - openMinutes);
            return workingMinutes > 0 && plannedDurationMinutes > workingMinutes;
        }

        internal static bool IsInspectionService(StandardService service)
        {
            var code = NormalizeText(service.Code ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);
            return code.StartsWith("dg", StringComparison.Ordinal);
        }

        internal static bool IsInspectionPackage(Package package)
        {
            var code = NormalizeText(package.Code ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            return code.StartsWith("dg", StringComparison.Ordinal);
        }
    }
}
