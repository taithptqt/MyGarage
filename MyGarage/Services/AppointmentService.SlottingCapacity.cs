using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<int> ResolveFixedDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            DateTime? requestedEnd = null,
            DateTime? requestedStart = null)
        {
            if (requestedStart.HasValue && requestedEnd.HasValue && requestedEnd > requestedStart)
            {
                return RoundUpToStep((int)Math.Ceiling((requestedEnd.Value - requestedStart.Value).TotalMinutes), slotStepMinutes);
            }

            var serviceIds = ParseServiceIds(requestedServiceIds).ToList();
            if (!serviceIds.Any()) return RoundUpToStep(60 + slotStepMinutes, slotStepMinutes);
            var minutes = await _context.StandardServices.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).SumAsync(s => (int?)s.StandardMinutes) ?? 60;
            return RoundUpToStep(minutes + slotStepMinutes, slotStepMinutes);
        }
    }
}
