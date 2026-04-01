using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<string?> ResolveRequestedServiceIdsForBookingAsync(string? requestedServiceIds)
        {
            var normalizedIds = ParseServiceIds(requestedServiceIds)
                .Distinct()
                .ToList();

            if (normalizedIds.Count > 0)
            {
                return string.Join(",", normalizedIds);
            }

            var defaultDiagnosticServiceId = await ResolveDefaultDiagnosticServiceIdAsync();
            return defaultDiagnosticServiceId.HasValue
                ? defaultDiagnosticServiceId.Value.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        private async Task<long?> ResolveDefaultDiagnosticServiceIdAsync()
        {
            var candidates = await _context.StandardServices
                .AsNoTracking()
                .Where(s => s.IsDeleted != true && s.IsActive == true)
                .Select(s => new { s.Id, s.Code, s.Name })
                .ToListAsync();

            var byCode = candidates.FirstOrDefault(s =>
                string.Equals(s.Code?.Trim(), DefaultDiagnosticServiceCode, StringComparison.OrdinalIgnoreCase));
            if (byCode != null)
            {
                return byCode.Id;
            }

            var byName = candidates.FirstOrDefault(s =>
                NormalizeText(s.Name).Contains("chuan doan tieu chuan"));
            if (byName != null)
            {
                return byName.Id;
            }

            var fallbackDiagnostic = candidates.FirstOrDefault(s =>
                NormalizeText(s.Name).Contains("chuan doan"));
            return fallbackDiagnostic?.Id;
        }

        private async Task<ServiceCluster> ResolveServiceClusterAsync(string? requestedServiceIds, string? notes)
        {
            var ids = ParseServiceIds(requestedServiceIds).ToList();
            if (!ids.Any()) return IsHeavyHint(notes) ? ServiceCluster.Heavy : ServiceCluster.Quick;

            var services = await _context.StandardServices
                .AsNoTracking()
                .Include(s => s.Category)
                .Where(s => ids.Contains(s.Id))
                .ToListAsync();

            var heavy = services.Any(s => IsHeavyHint(s.Name) || IsHeavyHint(s.Category?.Name));
            return heavy ? ServiceCluster.Heavy : ServiceCluster.Quick;
        }
    }
}
