using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        private async Task<long?> ResolveNearestBranchIdAsync(decimal? lat, decimal? lng, string? city = null)
        {
            var activeBranches = await _context.Branches
                .Where(b => b.IsDeleted != true)
                .Select(b => new { b.Id, b.Name, b.Lat, b.Lng })
                .ToListAsync();

            if (activeBranches.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(city) && TryResolveRegionFromProvince(city, out var region))
            {
                var regionKeywords = region switch
                {
                    "north" => new[] { "ha noi", "hanoi", "bac" },
                    "central" => new[] { "da nang", "danang", "trung" },
                    "south" => new[] { "hcm", "ho chi minh", "sai gon", "nam" },
                    _ => Array.Empty<string>()
                };

                var branchByRegion = activeBranches.FirstOrDefault(b =>
                    !string.IsNullOrWhiteSpace(b.Name) &&
                    regionKeywords.Any(k => NormalizeVietnamText(b.Name!).Contains(k, StringComparison.Ordinal)));

                if (branchByRegion is not null)
                {
                    return branchByRegion.Id;
                }
            }

            if (lat.HasValue && lng.HasValue)
            {
                var geoBranches = activeBranches
                    .Where(b => b.Lat.HasValue && b.Lng.HasValue)
                    .ToList();

                if (geoBranches.Count > 0)
                {
                    var nearest = geoBranches
                        .Select(b => new
                        {
                            b.Id,
                            Distance = CalculateDistanceKm(
                                (double)lat.Value,
                                (double)lng.Value,
                                (double)b.Lat!.Value,
                                (double)b.Lng!.Value)
                        })
                        .OrderBy(x => x.Distance)
                        .First();

                    return nearest.Id;
                }
            }

            return activeBranches.OrderBy(b => b.Id).Select(b => (long?)b.Id).FirstOrDefault();
        }

        private async Task<string> GetBranchNameAsync(long? branchId)
        {
            if (!branchId.HasValue) return string.Empty;
            return await _context.Branches
                       .Where(b => b.Id == branchId.Value)
                       .Select(b => b.Name ?? string.Empty)
                       .FirstOrDefaultAsync()
                   ?? string.Empty;
        }
    }
}
