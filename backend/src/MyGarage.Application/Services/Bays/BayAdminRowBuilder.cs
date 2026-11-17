using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Common;

namespace MyGarage.Application.Bays;

internal static class BayAdminRowBuilder
{
    public static async Task<List<BayViewRow>> BuildBayRowsAsync(
        IBayAdminReadDataService dataService,
        IAppConfigFileStore configStore,
        bool includeHidden,
        BayAdminSettings? settings = null,
        DateTime? nowUtc = null,
        CancellationToken ct = default)
    {
        settings ??= await BayAdminSettingsHelper.ReadSettingsAsync(configStore, ct);
        var now = nowUtc ?? VietnamTime.Now;

        var bays = await dataService.GetBaysWithBranchAsync(includeHidden, ct);

        var usageMap = BuildUsageMetrics(settings, now);
        var activeVehicleIds = new HashSet<long>();
        foreach (var bay in bays)
        {
            if (bay.CurrentOccupiedVehicleId.HasValue)
            {
                activeVehicleIds.Add(bay.CurrentOccupiedVehicleId.Value);
            }

            if (usageMap.TryGetValue(bay.Id, out var usage))
            {
                foreach (var session in usage.ActiveSessions)
                {
                    activeVehicleIds.Add(session.VehicleId);
                }
            }
        }

        var vehicleMap = await dataService.LoadVehicleMapAsync(activeVehicleIds, ct);
        var rows = new List<BayViewRow>(bays.Count);

        foreach (var bay in bays)
        {
            usageMap.TryGetValue(bay.Id, out var usage);
            usage ??= new BayUsageMetrics();

            var metadata = BayAdminSettingsHelper.ResolveMetadata(settings, bay.Id);
            var activeSessionByVehicle = usage.ActiveSessions
                .GroupBy(s => s.VehicleId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First());

            var activeVehicleIdsByBay = usage.ActiveSessions
                .Select(s => s.VehicleId)
                .ToHashSet();
            if (bay.CurrentOccupiedVehicleId.HasValue)
            {
                activeVehicleIdsByBay.Add(bay.CurrentOccupiedVehicleId.Value);
            }

            var activeVehicles = activeVehicleIdsByBay
                .Select(vehicleId =>
                {
                    vehicleMap.TryGetValue(vehicleId, out var vehicle);
                    activeSessionByVehicle.TryGetValue(vehicleId, out var session);

                    var occupiedMinutes = session != null
                        ? Math.Max(0, Math.Round((now - session.AssignedAt).TotalMinutes))
                        : 0;

                    return new BayVehicleView
                    {
                        VehicleId = vehicleId,
                        LicensePlate = vehicle?.LicensePlate ?? $"#{vehicleId}",
                        ModelName = vehicle?.ModelName,
                        BrandName = vehicle?.BrandName,
                        Segment = vehicle?.Segment,
                        AssignedAt = session?.AssignedAt,
                        OccupiedMinutes = (int)occupiedMinutes
                    };
                })
                .OrderBy(v => v.LicensePlate)
                .ToList();

            var status = BayAdminSettingsHelper.NormalizeOperationalStatus(bay.OperationalStatus);

            var occupiedCount = activeVehicles.Count;
            var capacity = metadata.Capacity;
            var utilizationRate = capacity > 0
                ? Math.Round(occupiedCount * 100d / capacity, 2)
                : 0;

            rows.Add(new BayViewRow
            {
                Id = bay.Id,
                BranchId = bay.BranchId,
                BranchName = bay.Branch?.Name ?? $"Branch #{bay.BranchId}",
                SequenceOrder = bay.SequenceOrder,
                Code = bay.Code,
                Name = bay.Name,
                BayType = bay.BayType,
                Area = metadata.Area,
                Capacity = capacity,
                SupportedSegments = metadata.SupportedSegments,
                MaxTonnage = bay.MaxTonnage,
                OperationalStatus = status,
                IsLocked = bay.IsActive == false,
                IsHidden = bay.IsDeleted == true,
                OccupiedCount = occupiedCount,
                UtilizationRate = utilizationRate,
                SessionCount = usage.SessionCount,
                TotalUsageHours = usage.TotalUsageHours,
                LastUsedAt = usage.LastUsedAt,
                LongestActiveHours = usage.LongestActiveHours,
                VehicleHistoryCount = usage.VehicleHistoryCount,
                ActiveVehicles = activeVehicles
            });
        }

        return rows;
    }

    public static Dictionary<long, BayUsageMetrics> BuildUsageMetrics(BayAdminSettings settings, DateTime nowUtc)
    {
        var grouped = settings.Sessions
            .Where(s => s.BayId > 0)
            .GroupBy(s => s.BayId);

        var map = new Dictionary<long, BayUsageMetrics>();

        foreach (var group in grouped)
        {
            var sessions = group
                .Where(s => s.AssignedAt != default)
                .OrderBy(s => s.AssignedAt)
                .ToList();

            var metric = new BayUsageMetrics();

            foreach (var session in sessions)
            {
                var endTime = session.ReleasedAt ?? nowUtc;
                if (endTime < session.AssignedAt)
                {
                    endTime = session.AssignedAt;
                }

                var durationHours = (endTime - session.AssignedAt).TotalHours;
                metric.SessionCount += 1;
                metric.TotalUsageHours += durationHours;
                metric.VehicleIds.Add(session.VehicleId);

                var lastUsedCandidate = session.ReleasedAt ?? session.AssignedAt;
                if (!metric.LastUsedAt.HasValue || lastUsedCandidate > metric.LastUsedAt.Value)
                {
                    metric.LastUsedAt = lastUsedCandidate;
                }

                if (session.ReleasedAt == null)
                {
                    metric.ActiveSessions.Add(session);
                    if (durationHours > metric.LongestActiveHours)
                    {
                        metric.LongestActiveHours = durationHours;
                    }
                }
            }

            metric.VehicleHistoryCount = metric.VehicleIds.Count;
            map[group.Key] = metric;
        }

        return map;
    }

    public static object MapBayListItem(BayViewRow x)
    {
        return new
        {
            id = x.Id,
            branchId = x.BranchId,
            branchName = x.BranchName,
            sequenceOrder = x.SequenceOrder,
            code = x.Code,
            name = x.Name,
            bayType = x.BayType,
            area = x.Area,
            capacity = x.Capacity,
            supportedSegments = x.SupportedSegments,
            maxTonnage = x.MaxTonnage,
            operationalStatus = x.OperationalStatus,
            isLocked = x.IsLocked,
            isHidden = x.IsHidden,
            occupiedCount = x.OccupiedCount,
            utilizationRate = Math.Round(x.UtilizationRate, 2),
            sessionCount = x.SessionCount,
            totalUsageHours = Math.Round(x.TotalUsageHours, 2),
            lastUsedAt = x.LastUsedAt,
            longestActiveHours = Math.Round(x.LongestActiveHours, 2),
            vehicleHistoryCount = x.VehicleHistoryCount,
            activeVehicles = x.ActiveVehicles.Select(v => new
            {
                vehicleId = v.VehicleId,
                licensePlate = v.LicensePlate,
                modelName = v.ModelName,
                brandName = v.BrandName,
                segment = v.Segment,
                assignedAt = v.AssignedAt,
                occupiedMinutes = v.OccupiedMinutes
            })
        };
    }

    public static async Task<long?> FindVehicleActiveBayAsync(
        IBayVehicleLookupDataService dataService,
        long vehicleId,
        BayAdminSettings settings,
        long? ignoreBayId = null,
        CancellationToken ct = default)
    {
        var bySession = settings.Sessions
            .Where(s => s.VehicleId == vehicleId && s.ReleasedAt == null && (!ignoreBayId.HasValue || s.BayId != ignoreBayId.Value))
            .OrderByDescending(s => s.AssignedAt)
            .Select(s => (long?)s.BayId)
            .FirstOrDefault();

        if (bySession.HasValue) return bySession;

        return await dataService.FindOccupiedBayIdByVehicleAsync(vehicleId, ignoreBayId, ct);
    }
}
