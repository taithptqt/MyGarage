using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Common;

namespace MyGarage.Application.Bays;

public sealed class BayAdminQueryService : IBayAdminQueryService
{
    private readonly IBayAdminReadDataService _dataService;
    private readonly IBayAdminAnalyticsService _analyticsService;
    private readonly IAppConfigFileStore _configStore;

    public BayAdminQueryService(
        IBayAdminReadDataService dataService,
        IBayAdminAnalyticsService analyticsService,
        IAppConfigFileStore configStore)
    {
        _dataService = dataService;
        _analyticsService = analyticsService;
        _configStore = configStore;
    }

    public async Task<BayAdminCommandResult> GetLookupsAsync(
        int takeVehicles,
        CancellationToken ct = default)
    {
        takeVehicles = Math.Clamp(takeVehicles, 50, 5000);

        var branchRows = await _dataService.GetLookupBranchesAsync(ct);
        var branches = branchRows.Select(b => new
        {
            id = b.Id,
            name = b.Name,
            status = b.Status
        }).ToList();

        var vehicleRows = await _dataService.GetLookupVehiclesAsync(takeVehicles, ct);
        var vehicles = vehicleRows.Select(v => new
        {
            id = v.Id,
            licensePlate = v.LicensePlate,
            modelName = v.ModelName,
            brandName = v.BrandName,
            segment = v.Segment
        }).ToList();

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        var areaOptions = settings.BayMetadata.Values
            .Select(m => BayAdminSettingsHelper.NormalizeArea(m.Area))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a)
            .ToList();

        return BayAdminCommandResult.Ok(new
        {
            branches,
            vehicles,
            areaOptions,
            statusOptions = new[]
            {
                BayAdminConstants.StatusActive,
                BayAdminConstants.StatusUnderMaintenance,
                BayAdminConstants.StatusBroken
            }
        });
    }

    public async Task<BayAdminCommandResult> GetListAsync(
        long? branchId,
        string? search,
        string? area,
        string? status,
        string? sortBy,
        string? sortDirection,
        bool includeHidden,
        CancellationToken ct = default)
    {
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: true, ct: ct);
        var query = rows.AsEnumerable();

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (!includeHidden)
        {
            query = query.Where(x => !x.IsHidden);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = BayAdminSettingsHelper.NormalizeForSearch(search);
            query = query.Where(x =>
                BayAdminSettingsHelper.NormalizeForSearch(x.Code).Contains(normalized) ||
                BayAdminSettingsHelper.NormalizeForSearch(x.Name).Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            var normalizedArea = BayAdminSettingsHelper.NormalizeForSearch(area);
            query = query.Where(x => BayAdminSettingsHelper.NormalizeForSearch(x.Area).Contains(normalizedArea));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = BayAdminSettingsHelper.NormalizeOperationalStatus(status);
            query = query.Where(x => x.OperationalStatus == normalizedStatus);
        }

        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        var normalizedSort = BayAdminSettingsHelper.NormalizeKey(sortBy);

        query = normalizedSort switch
        {
            "USAGE" => direction == "desc"
                ? query.OrderByDescending(x => x.SessionCount).ThenByDescending(x => x.TotalUsageHours).ThenBy(x => x.SequenceOrder).ThenBy(x => x.Code)
                : query.OrderBy(x => x.SessionCount).ThenBy(x => x.TotalUsageHours).ThenBy(x => x.SequenceOrder).ThenBy(x => x.Code),
            "SEQUENCE" or "ORDER" => direction == "desc"
                ? query.OrderByDescending(x => x.SequenceOrder).ThenByDescending(x => x.Code).ThenByDescending(x => x.Name)
                : query.OrderBy(x => x.SequenceOrder).ThenBy(x => x.Code).ThenBy(x => x.Name),
            _ => direction == "desc"
                ? query.OrderByDescending(x => x.Code).ThenByDescending(x => x.Name)
                : query.OrderBy(x => x.Code).ThenBy(x => x.Name)
        };

        var items = query.Select(BayAdminRowBuilder.MapBayListItem).ToList();
        return BayAdminCommandResult.Ok(new
        {
            items,
            total = items.Count
        });
    }

    public async Task<BayAdminCommandResult> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: true, ct: ct);
        var bay = rows.FirstOrDefault(x => x.Id == id);
        if (bay == null)
        {
            return BayAdminCommandResult.NotFound("Khoang không tồn tại.");
        }

        return BayAdminCommandResult.Ok(BayAdminRowBuilder.MapBayListItem(bay));
    }

    public async Task<BayAdminCommandResult> GetSummaryAsync(
        long? branchId,
        CancellationToken ct = default)
    {
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: true, ct: ct);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var visible = rows.Where(x => !x.IsHidden).ToList();
        var totalCapacity = visible.Sum(x => x.Capacity);
        var totalOccupied = visible.Sum(x => x.OccupiedCount);
        var fillRate = totalCapacity > 0 ? Math.Round(totalOccupied * 100d / totalCapacity, 2) : 0d;

        var mostActive = visible
            .OrderByDescending(x => x.SessionCount)
            .ThenByDescending(x => x.TotalUsageHours)
            .FirstOrDefault();

        var longestOccupied = visible
            .Where(x => x.LongestActiveHours > 0)
            .OrderByDescending(x => x.LongestActiveHours)
            .FirstOrDefault();

        var byArea = visible
            .GroupBy(x => x.Area)
            .Select(g =>
            {
                var groupCapacity = g.Sum(x => x.Capacity);
                var groupOccupied = g.Sum(x => x.OccupiedCount);
                return new
                {
                    area = g.Key,
                    totalBays = g.Count(),
                    totalCapacity = groupCapacity,
                    occupiedVehicles = groupOccupied,
                    fillRate = groupCapacity > 0 ? Math.Round(groupOccupied * 100d / groupCapacity, 2) : 0d,
                    usageRate = g.Count() > 0 ? Math.Round(g.Count(x => x.SessionCount > 0) * 100d / g.Count(), 2) : 0d
                };
            })
            .OrderByDescending(x => x.fillRate)
            .ThenBy(x => x.area)
            .ToList();

        var byBranch = visible
            .GroupBy(x => new { x.BranchId, x.BranchName })
            .Select(g =>
            {
                var groupCapacity = g.Sum(x => x.Capacity);
                var groupOccupied = g.Sum(x => x.OccupiedCount);
                return new
                {
                    branchId = g.Key.BranchId,
                    branchName = g.Key.BranchName,
                    totalBays = g.Count(),
                    totalCapacity = groupCapacity,
                    occupiedVehicles = groupOccupied,
                    fillRate = groupCapacity > 0 ? Math.Round(groupOccupied * 100d / groupCapacity, 2) : 0d,
                    usageRate = g.Count() > 0 ? Math.Round(g.Count(x => x.SessionCount > 0) * 100d / g.Count(), 2) : 0d
                };
            })
            .OrderByDescending(x => x.fillRate)
            .ThenBy(x => x.branchName)
            .ToList();

        return BayAdminCommandResult.Ok(new
        {
            totalBays = visible.Count,
            hiddenBays = rows.Count(x => x.IsHidden),
            emptyBays = visible.Count(x => x.OperationalStatus == BayAdminConstants.StatusActive && x.OccupiedCount == 0),
            inUseBays = visible.Count(x => x.OperationalStatus == BayAdminConstants.StatusActive && x.OccupiedCount > 0),
            maintenanceBays = visible.Count(x => x.OperationalStatus == BayAdminConstants.StatusUnderMaintenance),
            suspendedBays = visible.Count(x => x.OperationalStatus == BayAdminConstants.StatusBroken),
            lockedBays = visible.Count(x => x.IsLocked),
            totalCapacity,
            occupiedVehicles = totalOccupied,
            fillRate,
            mostActiveBay = mostActive == null ? null : new
            {
                id = mostActive.Id,
                code = mostActive.Code,
                name = mostActive.Name,
                branchName = mostActive.BranchName,
                sessionCount = mostActive.SessionCount
            },
            longestOccupiedBay = longestOccupied == null ? null : new
            {
                id = longestOccupied.Id,
                code = longestOccupied.Code,
                name = longestOccupied.Name,
                branchName = longestOccupied.BranchName,
                occupiedHours = Math.Round(longestOccupied.LongestActiveHours, 2)
            },
            utilizationByArea = byArea,
            utilizationByBranch = byBranch
        });
    }

    public async Task<BayAdminCommandResult> GetBayVehiclesAsync(
        long id,
        CancellationToken ct = default)
    {
        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: true, settings: settings, ct: ct);
        var bay = rows.FirstOrDefault(x => x.Id == id);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        return BayAdminCommandResult.Ok(new
        {
            bay = BayAdminRowBuilder.MapBayListItem(bay),
            vehicles = bay.ActiveVehicles.Select(v => new
            {
                vehicleId = v.VehicleId,
                licensePlate = v.LicensePlate,
                modelName = v.ModelName,
                brandName = v.BrandName,
                segment = v.Segment,
                assignedAt = v.AssignedAt,
                occupiedMinutes = v.OccupiedMinutes
            }),
            occupiedCount = bay.OccupiedCount,
            capacity = bay.Capacity,
            hasSpace = bay.OccupiedCount < bay.Capacity
        });
    }

    public async Task<BayAdminCommandResult> GetBayHistoryAsync(
        long id,
        int take,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 20, 2000);

        var bayExists = await _dataService.BayExistsAsync(id, ct);
        if (!bayExists) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        var sessions = settings.Sessions
            .Where(s => s.BayId == id)
            .OrderByDescending(s => s.AssignedAt)
            .Take(take)
            .ToList();

        var vehicleMap = await _dataService.LoadVehicleMapAsync(sessions.Select(s => s.VehicleId), ct);
        var now = VietnamTime.Now;

        var items = sessions.Select(s =>
        {
            vehicleMap.TryGetValue(s.VehicleId, out var vehicle);
            var durationHours = Math.Max(0, ((s.ReleasedAt ?? now) - s.AssignedAt).TotalHours);
            return new
            {
                sessionId = s.SessionId,
                vehicleId = s.VehicleId,
                licensePlate = vehicle?.LicensePlate ?? $"#{s.VehicleId}",
                modelName = vehicle?.ModelName,
                brandName = vehicle?.BrandName,
                segment = vehicle?.Segment,
                assignedAt = s.AssignedAt,
                releasedAt = s.ReleasedAt,
                serviceStatus = s.ServiceStatus,
                note = s.Note,
                durationHours = Math.Round(durationHours, 2),
                isActive = s.ReleasedAt == null
            };
        }).ToList();

        return BayAdminCommandResult.Ok(new
        {
            items,
            totalSessions = items.Count,
            uniqueVehicleCount = items.Select(x => x.vehicleId).Distinct().Count(),
            totalUsageHours = Math.Round(items.Sum(x => x.durationHours), 2)
        });
    }

    public Task<BayAdminCommandResult> GetUsageAsync(long? branchId, CancellationToken ct = default)
        => _analyticsService.GetUsageAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetLayoutAsync(long? branchId, CancellationToken ct = default)
        => _analyticsService.GetLayoutAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetAlertsAsync(long? branchId, CancellationToken ct = default)
        => _analyticsService.GetAlertsAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        CancellationToken ct = default)
        => _analyticsService.GetSuggestionsAsync(vehicleId, branchId, preferredArea, sourceBayId, serviceStatus, ct);
}
