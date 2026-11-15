using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Common;
using MyGarage.Constants;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentFlowResolutionService : IAppointmentFlowResolutionService
    {
        private readonly IAppointmentStore _store;

        public AppointmentFlowResolutionService(IAppointmentStore store)
        {
            _store = store;
        }

        public async Task<BookingFlowType> ResolveFlowAsync(
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

        public BookingFlowType ResolveFlowByAppointment(Appointment appointment)
        {
            if (!string.IsNullOrWhiteSpace(appointment.CustomerNotes) &&
                appointment.CustomerNotes.Contains(AppointmentNoteMarkers.FlowFlexible, StringComparison.OrdinalIgnoreCase))
            {
                return BookingFlowType.Flexible;
            }

            if (string.IsNullOrWhiteSpace(appointment.RequestedServiceIds) &&
                string.IsNullOrWhiteSpace(appointment.RequestedPackageIds))
            {
                return BookingFlowType.Flexible;
            }

            return BookingFlowType.Fixed;
        }

        public async Task<string?> ResolveRequestedServiceIdsForBookingAsync(
            string? requestedServiceIds,
            string? requestedPackageIds = null)
        {
            var normalizedIds = ParseIds(requestedServiceIds)
                .Distinct()
                .ToList();

            if (normalizedIds.Count > 0)
            {
                return string.Join(",", normalizedIds);
            }

            if (ParseIds(requestedPackageIds).Any())
            {
                return null;
            }

            var defaultDiagnosticServiceId = await ResolveDefaultDiagnosticServiceIdAsync();
            return defaultDiagnosticServiceId.HasValue
                ? defaultDiagnosticServiceId.Value.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        public async Task<int> ResolveScheduledDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false,
            string? requestedPackageIds = null)
        {
            var breakdown = await ResolveDurationBreakdownAsync(requestedServiceIds, slotStepMinutes, isParallel, requestedPackageIds);
            return breakdown.TotalMinutes;
        }

        public async Task<(int ServiceMinutes, int SetupMinutes, int BufferMinutes, int TotalMinutes)> ResolveDurationBreakdownAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false,
            string? requestedPackageIds = null)
        {
            var normalizedStep = ResolveSlotStepMinutes(slotStepMinutes);
            var serviceMinutes = await ResolveServiceWorkMinutesAsync(requestedServiceIds, isParallel, requestedPackageIds);
            var setupMinutes = Math.Max(0, StandardCheckInSetupMinutes);
            var bufferMinutes = Math.Max(ExecutionBufferMinutes, normalizedStep);
            var totalMinutes = RoundUpToStep(serviceMinutes + setupMinutes + bufferMinutes, normalizedStep);
            return (serviceMinutes, setupMinutes, bufferMinutes, totalMinutes);
        }

        public int ResolveFlexibleWindowMinutes(int slotStepMinutes)
        {
            var normalizedStep = ResolveSlotStepMinutes(slotStepMinutes);
            var minimumWindow = Math.Max(StandardCheckInSetupMinutes + ExecutionBufferMinutes, normalizedStep);
            return RoundUpToStep(minimumWindow, normalizedStep);
        }

        public async Task<ServiceCluster> ResolveServiceClusterAsync(
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

        public async Task<IReadOnlyList<long>> ResolvePackageItemServiceIdsAsync(string? requestedPackageIds)
        {
            var packageIds = ParseIds(requestedPackageIds).Distinct().ToList();
            if (packageIds.Count == 0)
            {
                return Array.Empty<long>();
            }

            return await _store.GetPackageItemServiceIdsAsync(packageIds);
        }

        public async Task<List<Package>> LoadRequestedPackagesAsync(string? requestedPackageIds)
        {
            var ids = ParseIds(requestedPackageIds).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<Package>();
            }

            return await _store.GetActivePackagesByIdsAsync(ids, includeCategoryAndItems: true);
        }

        private async Task<int> ResolveServiceWorkMinutesAsync(
            string? requestedServiceIds,
            bool isParallel,
            string? requestedPackageIds = null)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds) && string.IsNullOrWhiteSpace(requestedPackageIds))
            {
                var defaultMinutes = await ResolveDefaultDiagnosticServiceMinutesAsync();
                return Math.Max(30, defaultMinutes);
            }

            var serviceGroups = SplitRequestedGroups(requestedServiceIds);
            var packageGroups = SplitRequestedGroups(requestedPackageIds);
            var groupCount = Math.Max(serviceGroups.Count, packageGroups.Count);
            if (groupCount == 0)
            {
                groupCount = 1;
            }

            var aggregateMinutes = 0;
            var hasExplicitServiceIds = false;

            for (var index = 0; index < groupCount; index++)
            {
                var serviceGroup = index < serviceGroups.Count ? serviceGroups[index] : string.Empty;
                var packageGroup = index < packageGroups.Count ? packageGroups[index] : string.Empty;
                var serviceIds = ParseIds(serviceGroup).ToList();
                var packageIds = ParseIds(packageGroup).ToList();
                if (!serviceIds.Any() && !packageIds.Any())
                {
                    aggregateMinutes = isParallel
                        ? Math.Max(aggregateMinutes, 60)
                        : aggregateMinutes + 60;
                    continue;
                }

                hasExplicitServiceIds = true;

                var serviceRows = await _store.GetActiveStandardServiceLookupsByIdsAsync(serviceIds);

                var groupMinutes = serviceRows
                    .Where(s => !IsMiniCheckService(s.Code, s.Name))
                    .Sum(s => Math.Max(0, s.StandardMinutes));

                if (serviceRows.Count == 0)
                {
                    groupMinutes = packageIds.Count > 0 ? 0 : 60;
                }

                groupMinutes += await ResolvePackageWorkMinutesAsync(packageIds);
                if (groupMinutes == 0 && packageIds.Count > 0)
                {
                    groupMinutes = 60;
                }

                aggregateMinutes = isParallel
                    ? Math.Max(aggregateMinutes, groupMinutes)
                    : aggregateMinutes + groupMinutes;
            }

            if (aggregateMinutes <= 0)
            {
                aggregateMinutes = hasExplicitServiceIds ? 0 : 60;
            }

            return aggregateMinutes;
        }

        private async Task<int> ResolvePackageWorkMinutesAsync(IReadOnlyCollection<long> packageIds)
        {
            if (packageIds.Count == 0)
            {
                return 0;
            }

            var packages = await _store.GetActivePackagesByIdsAsync(packageIds, includeCategoryAndItems: false);

            var minutes = 0;
            foreach (var package in packages)
            {
                if (package.StandardMinutes > 0)
                {
                    minutes += package.StandardMinutes;
                    continue;
                }

                minutes += package.PackageItems
                    .Where(item => item.StandardService != null && !IsMiniCheckService(item.StandardService.Code, item.StandardService.Name))
                    .Sum(item => Math.Max(0, (int)Math.Ceiling(item.StandardService.StandardMinutes * Math.Max(1m, item.Quantity))));
            }

            return minutes;
        }

        private async Task<long?> ResolveDefaultDiagnosticServiceIdAsync()
        {
            var candidates = await _store.GetActiveStandardServiceLookupsAsync();

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

        private async Task<int> ResolveDefaultDiagnosticServiceMinutesAsync()
        {
            var defaultDiagnosticServiceId = await ResolveDefaultDiagnosticServiceIdAsync();
            if (!defaultDiagnosticServiceId.HasValue)
            {
                return 60;
            }

            var minutes = await _store.GetStandardServiceMinutesAsync(defaultDiagnosticServiceId.Value);
            return Math.Max(30, minutes ?? 60);
        }

        private async Task<List<StandardService>> LoadRequestedServicesAsync(string? requestedServiceIds)
        {
            var ids = ParseIds(requestedServiceIds).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<StandardService>();
            }

            return await _store.GetActiveStandardServicesByIdsAsync(ids, includeCategory: true);
        }

        private async Task<List<StandardService>> LoadRequestedServicesWithPackageItemsAsync(
            string? requestedServiceIds,
            string? requestedPackageIds)
        {
            var serviceIds = ParseIds(requestedServiceIds)
                .Concat(await ResolvePackageItemServiceIdsAsync(requestedPackageIds))
                .Distinct()
                .ToList();

            if (serviceIds.Count == 0)
            {
                return new List<StandardService>();
            }

            return await _store.GetActiveStandardServicesByIdsAsync(serviceIds, includeCategory: true);
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

        private static bool IsPackageHeavySignal(Package package)
        {
            if (InsuranceBillingRules.IsInsuranceSignalPackage(package))
            {
                return true;
            }

            var text = NormalizeText($"{package.Code} {package.Name} {package.Category?.Code} {package.Category?.Name}");
            return IsHeavyHint(text)
                   || text.Contains("body")
                   || text.Contains("paint")
                   || text.Contains("dong son")
                   || text.Contains("tai nan")
                   || text.Contains("phuc hoi");
        }

        private static IReadOnlyList<string> SplitRequestedGroups(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(group => group.Trim())
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .ToList();
        }
    }
}
