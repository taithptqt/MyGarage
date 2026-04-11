using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
        public partial class AppointmentService
    {
        private async Task<BookingFlowType> ResolveFlowAsync(
            string? bookingFlow,
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            var explicitFlow = ParseExplicitFlow(bookingFlow);
            if (explicitFlow.HasValue)
            {
                return explicitFlow.Value;
            }

            var services = await LoadRequestedServicesAsync(requestedServiceIds);
            var packages = await LoadRequestedPackagesAsync(requestedPackageIds);

            if (services.Count > 0)
            {
                return services.Any(IsInspectionService)
                    ? BookingFlowType.Flexible
                    : BookingFlowType.Fixed;
            }

            if (packages.Count > 0)
            {
                return packages.Any(IsInspectionPackage)
                    ? BookingFlowType.Flexible
                    : BookingFlowType.Fixed;
            }

            return BookingFlowType.Flexible;
        }

        private async Task<string?> ResolveRequestedServiceIdsForBookingAsync(
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            var normalizedIds = ParseServiceIds(requestedServiceIds)
                .Distinct()
                .ToList();

            if (normalizedIds.Count > 0)
            {
                return string.Join(",", normalizedIds);
            }

            if (ParsePackageIds(requestedPackageIds).Any())
            {
                return null;
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

        private static string BuildFlexibleDynamicNotice(
            int serviceMinutes,
            int setupMinutes,
            int bufferMinutes,
            int estimatedMinutes)
        {
            return $"T\u1ED5ng th\u1EDDi gian d\u1EF1 ki\u1EBFn {estimatedMinutes} ph\u00FAt " +
                   $"(s\u1EEDa ch\u1EEFa {serviceMinutes}p + ti\u1EBFp nh\u1EADn/ch\u1EE5p \u1EA3nh {setupMinutes}p + buffer {bufferMinutes}p). " +
                   "Th\u1EDDi gian ho\u00E0n t\u1EA5t th\u1EF1c t\u1EBF s\u1EBD \u0111\u01B0\u1EE3c c\u1EADp nh\u1EADt sau khi k\u1EF9 thu\u1EADt vi\u00EAn ho\u00E0n t\u1EA5t b\u01B0\u1EDBc ki\u1EC3m tra ban \u0111\u1EA7u.";
        }

        private async Task<List<StandardService>> LoadRequestedServicesAsync(string? requestedServiceIds)
        {
            var ids = ParseServiceIds(requestedServiceIds).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<StandardService>();
            }

            return await _context.StandardServices
                .AsNoTracking()
                .Include(s => s.Category)
                .Where(s => ids.Contains(s.Id) && s.IsDeleted != true && s.IsActive == true)
                .ToListAsync();
        }

        private async Task<int> ResolveDefaultDiagnosticServiceMinutesAsync()
        {
            var defaultDiagnosticServiceId = await ResolveDefaultDiagnosticServiceIdAsync();
            if (!defaultDiagnosticServiceId.HasValue)
            {
                return 60;
            }

            var minutes = await _context.StandardServices
                .AsNoTracking()
                .Where(s => s.Id == defaultDiagnosticServiceId.Value)
                .Select(s => (int?)s.StandardMinutes)
                .FirstOrDefaultAsync();

            return Math.Max(30, minutes ?? 60);
        }

        private static BookingFlowType? ParseExplicitFlow(string? bookingFlow)
        {
            if (string.IsNullOrWhiteSpace(bookingFlow))
            {
                return null;
            }

            var key = NormalizeText(bookingFlow);
            if (key.Contains("flex") || key.Contains("window") || key.Contains("check"))
            {
                return BookingFlowType.Flexible;
            }

            if (key.Contains("fixed") || key.Contains("slot"))
            {
                return BookingFlowType.Fixed;
            }

            return null;
        }

        private static bool IsInspectionService(StandardService service)
        {
            var code = NormalizeText(service.Code ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);
            return code.StartsWith("dg", StringComparison.Ordinal);
        }

        private static bool IsMiniCheckService(string? code, string? name, string? categoryName = null)
        {
            var normalizedCode = NormalizeText(code ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            if (normalizedCode is "mini10" or "minicheck" or "mini" || normalizedCode.StartsWith("mnc", StringComparison.Ordinal))
            {
                return true;
            }

            var text = NormalizeText($"{code} {name} {categoryName}");
            return text.Contains("mini check")
                   || text.Contains("mini-check")
                   || text.Contains("minicheck");
        }

        private async Task<ServiceCluster> ResolveServiceClusterAsync(string? requestedServiceIds, string? notes)
        {
            return await ResolveServiceClusterAsync(requestedServiceIds, null, notes);
        }

        private async Task<ServiceCluster> ResolveServiceClusterAsync(
            string? requestedServiceIds,
            string? requestedPackageIds,
            string? notes)
        {
            var services = await LoadRequestedServicesWithPackageItemsAsync(requestedServiceIds, requestedPackageIds);
            var packages = await LoadRequestedPackagesAsync(requestedPackageIds);
            if (services.Count == 0 && packages.Count == 0)
            {
                return IsHeavyHint(notes) ? ServiceCluster.Heavy : ServiceCluster.Quick;
            }

            var heavy = services.Any(s => IsHeavyHint(s.Name) || IsHeavyHint(s.Category?.Name));
            heavy = heavy || packages.Any(IsPackageHeavySignal);
            return heavy ? ServiceCluster.Heavy : ServiceCluster.Quick;
        }
    }
}
