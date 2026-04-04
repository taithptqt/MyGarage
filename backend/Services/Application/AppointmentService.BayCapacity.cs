using System.Collections.Generic;
using System.Linq;
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
            return IsQuickArea(area);
        }

        private static bool IsQuickSlotType(string? slotType, string? notes)
        {
            var key = NormalizeText($"{slotType} {notes}");
            return key.Contains("quick") || key.Contains("maintenance") || key.Contains("bao duong") || key.Contains("dich vu nhanh");
        }

        private static bool IsRepairBay(Bay bay)
        {
            if (bay == null) return false;
            var area = NormalizeText(bay.ServiceArea ?? string.Empty);
            return IsRepairArea(area);
        }

        private static bool IsQuickArea(string normalizedServiceArea)
        {
            if (string.IsNullOrWhiteSpace(normalizedServiceArea)) return false;
            return normalizedServiceArea == "quick";
        }

        private static bool IsRepairArea(string normalizedServiceArea)
        {
            if (string.IsNullOrWhiteSpace(normalizedServiceArea)) return false;
            return normalizedServiceArea == "repair";
        }
    }
}
