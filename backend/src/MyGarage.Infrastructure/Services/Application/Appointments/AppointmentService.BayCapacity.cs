using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static CapacityProfile ComputeCapacity(List<Bay> activeBays)
        {
            if (activeBays.Count == 0) return new CapacityProfile(8, 12, 20);
            int quick = activeBays.Count(IsQuickBay);
            int repair = activeBays.Count(IsRepairBay);
            return new CapacityProfile(quick, repair, quick + repair);
        }

        private static bool IsQuickBay(Bay bay)
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

        private static bool IsQuickSlotType(string? slotType, string? notes)
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

        private static bool IsBufferSlotType(string? slotType)
        {
            return string.Equals(slotType, nameof(WorkSlotType.BUFFER), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRepairBay(Bay bay)
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
    }
}
