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
            int quick = activeBays.Count(b => IsQuickBay(b.BayType));
            int repair = activeBays.Count - quick;
            if (quick == 0 && repair > 0)
            {
                quick = System.Math.Min(8, repair);
                repair -= quick;
            }
            return new CapacityProfile(quick, repair, quick + repair);
        }

        private static bool IsQuickBay(string? bayType)
        {
            if (string.IsNullOrWhiteSpace(bayType)) return false;
            var key = NormalizeText(bayType);
            return key.Contains("quick") || key.Contains("fast") || key.Contains("bao duong") || key.Contains("dich vu nhanh");
        }

        private static bool IsQuickSlotType(string? slotType, string? notes)
        {
            var key = NormalizeText($"{slotType} {notes}");
            return key.Contains("quick") || key.Contains("maintenance") || key.Contains("bao duong") || key.Contains("dich vu nhanh");
        }
    }
}
