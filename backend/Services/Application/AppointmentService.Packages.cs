using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static IEnumerable<long> ParsePackageIds(string? csv)
        {
            return ParseServiceIds(csv);
        }

        private static string? NormalizeRequestedIdsForStorage(string? raw)
        {
            var ids = ParseServiceIds(raw)
                .Distinct()
                .ToList();

            return ids.Count == 0
                ? null
                : string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        }

        private async Task<string?> ResolveRequestedPackageIdsForBookingAsync(string? requestedPackageIds)
        {
            var normalized = NormalizeRequestedIdsForStorage(requestedPackageIds);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var packageIds = ParsePackageIds(normalized).Distinct().ToList();
            var existingCount = await _context.Packages
                .AsNoTracking()
                .CountAsync(package => packageIds.Contains(package.Id) && package.IsDeleted != true && package.IsActive != false);

            if (existingCount != packageIds.Count)
            {
                throw new InvalidOperationException("Gói dịch vụ không hợp lệ hoặc đã ngừng áp dụng.");
            }

            return normalized;
        }

        private async Task<List<Package>> LoadRequestedPackagesAsync(string? requestedPackageIds)
        {
            var ids = ParsePackageIds(requestedPackageIds).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<Package>();
            }

            return await _context.Packages
                .AsNoTracking()
                .Include(package => package.Category)
                .Include(package => package.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                    .ThenInclude(item => item.StandardService)
                        .ThenInclude(service => service.Category)
                .Where(package => ids.Contains(package.Id) && package.IsDeleted != true && package.IsActive != false)
                .ToListAsync();
        }

        private async Task<IReadOnlyList<long>> ResolvePackageItemServiceIdsAsync(string? requestedPackageIds)
        {
            var packageIds = ParsePackageIds(requestedPackageIds).Distinct().ToList();
            if (packageIds.Count == 0)
            {
                return Array.Empty<long>();
            }

            return await _context.PackageItems
                .AsNoTracking()
                .Where(item =>
                    packageIds.Contains(item.PackageId) &&
                    item.IsDeleted != true &&
                    item.IsActive != false &&
                    item.StandardService.IsDeleted != true &&
                    item.StandardService.IsActive != false)
                .Select(item => item.StandardServiceId)
                .Distinct()
                .ToListAsync();
        }

        private async Task<List<StandardService>> LoadRequestedServicesWithPackageItemsAsync(
            string? requestedServiceIds,
            string? requestedPackageIds)
        {
            var serviceIds = ParseServiceIds(requestedServiceIds)
                .Concat(await ResolvePackageItemServiceIdsAsync(requestedPackageIds))
                .Distinct()
                .ToList();

            if (serviceIds.Count == 0)
            {
                return new List<StandardService>();
            }

            return await _context.StandardServices
                .AsNoTracking()
                .Include(service => service.Category)
                .Where(service => serviceIds.Contains(service.Id) && service.IsDeleted != true && service.IsActive == true)
                .ToListAsync();
        }

        private static bool IsInspectionPackage(Package package)
        {
            var code = NormalizeText(package.Code ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            return code.StartsWith("dg", StringComparison.Ordinal);
        }

        private static bool IsInsurancePackageSignal(Package package)
        {
            return InsuranceBillingRules.IsInsuranceSignalPackage(package);
        }

        private static bool IsPackageHeavySignal(Package package)
        {
            if (IsInsurancePackageSignal(package))
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

        private async Task<int> ResolvePackageWorkMinutesAsync(IReadOnlyCollection<long> packageIds)
        {
            if (packageIds.Count == 0)
            {
                return 0;
            }

            var packages = await _context.Packages
                .AsNoTracking()
                .Include(package => package.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                    .ThenInclude(item => item.StandardService)
                .Where(package => packageIds.Contains(package.Id) && package.IsDeleted != true && package.IsActive != false)
                .ToListAsync();

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
    }
}
