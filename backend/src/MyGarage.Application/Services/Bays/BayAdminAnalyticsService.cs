using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Common;

namespace MyGarage.Application.Bays;

public sealed class BayAdminAnalyticsService : IBayAdminAnalyticsService
{
    private readonly IBayAdminReadDataService _dataService;
    private readonly IAppConfigFileStore _configStore;

    public BayAdminAnalyticsService(IBayAdminReadDataService dataService, IAppConfigFileStore configStore)
    {
        _dataService = dataService;
        _configStore = configStore;
    }

    public async Task<BayAdminCommandResult> GetUsageAsync(
        long? branchId,
        CancellationToken ct = default)
    {
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: false, ct: ct);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var byBay = rows
            .OrderByDescending(x => x.SessionCount)
            .ThenByDescending(x => x.TotalUsageHours)
            .ThenBy(x => x.SequenceOrder)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                id = x.Id,
                sequenceOrder = x.SequenceOrder,
                code = x.Code,
                name = x.Name,
                branchId = x.BranchId,
                branchName = x.BranchName,
                area = x.Area,
                sessionCount = x.SessionCount,
                totalUsageHours = Math.Round(x.TotalUsageHours, 2),
                averageUsageHours = x.SessionCount > 0 ? Math.Round(x.TotalUsageHours / x.SessionCount, 2) : 0,
                longestActiveHours = Math.Round(x.LongestActiveHours, 2),
                occupiedCount = x.OccupiedCount,
                capacity = x.Capacity,
                utilizationRate = Math.Round(x.UtilizationRate, 2),
                lastUsedAt = x.LastUsedAt,
                vehicleHistoryCount = x.VehicleHistoryCount
            })
            .ToList();

        return BayAdminCommandResult.Ok(new
        {
            byBay,
            mostUsed = byBay.Take(5),
            leastUsed = byBay.OrderBy(x => x.sessionCount).ThenBy(x => x.totalUsageHours).ThenBy(x => x.sequenceOrder).ThenBy(x => x.code).Take(5),
            frequentOverload = byBay.Where(x => x.occupiedCount > x.capacity).OrderByDescending(x => x.occupiedCount - x.capacity).Take(5),
            longestOccupied = byBay.Where(x => x.longestActiveHours > 0).OrderByDescending(x => x.longestActiveHours).Take(5)
        });
    }

    public async Task<BayAdminCommandResult> GetLayoutAsync(
        long? branchId,
        CancellationToken ct = default)
    {
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: false, ct: ct);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var branches = rows
            .GroupBy(x => new { x.BranchId, x.BranchName })
            .Select(bg => new
            {
                branchId = bg.Key.BranchId,
                branchName = bg.Key.BranchName,
                totalBays = bg.Count(),
                areas = bg
                    .GroupBy(x => x.Area)
                    .Select(ag =>
                    {
                        var areaCapacity = ag.Sum(x => x.Capacity);
                        var areaOccupied = ag.Sum(x => x.OccupiedCount);
                        return new
                        {
                            area = ag.Key,
                            totalBays = ag.Count(),
                            totalCapacity = areaCapacity,
                            occupiedVehicles = areaOccupied,
                            densityPercent = areaCapacity > 0 ? Math.Round(areaOccupied * 100d / areaCapacity, 2) : 0d,
                            bays = ag
                                .OrderBy(x => x.SequenceOrder)
                                .ThenBy(x => x.Code)
                                .Select(x => new
                                {
                                    id = x.Id,
                                    sequenceOrder = x.SequenceOrder,
                                    code = x.Code,
                                    name = x.Name,
                                    status = x.OperationalStatus,
                                    occupiedCount = x.OccupiedCount,
                                    capacity = x.Capacity,
                                    isLocked = x.IsLocked,
                                    bayType = x.BayType
                                })
                                .ToList()
                        };
                    })
                    .OrderBy(a => a.area)
                    .ToList()
            })
            .OrderBy(x => x.branchName)
            .ToList();

        return BayAdminCommandResult.Ok(new
        {
            branches
        });
    }

    public async Task<BayAdminCommandResult> GetAlertsAsync(
        long? branchId,
        CancellationToken ct = default)
    {
        var now = VietnamTime.Now;
        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: false, settings: settings, nowUtc: now, ct: ct);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var longOccupancyThreshold = Math.Max(settings.AlertRules.LongOccupancyHours, 1);
        var inactiveThreshold = now.AddDays(-Math.Max(settings.AlertRules.InactiveDays, 1));

        var alerts = new List<BayAlertItem>();

        foreach (var bay in rows)
        {
            if (bay.OccupiedCount >= bay.Capacity)
            {
                alerts.Add(new BayAlertItem
                {
                    Type = "BAY_FULL",
                    Severity = bay.OccupiedCount > bay.Capacity ? "CRITICAL" : "HIGH",
                    BayId = bay.Id,
                    Code = bay.Code,
                    Name = bay.Name,
                    BranchName = bay.BranchName,
                    Area = bay.Area,
                    Message = bay.OccupiedCount > bay.Capacity
                        ? "Khoang vượt quá sức chứa tối đa."
                        : "Khoang đã đầy.",
                    CreatedAt = now
                });
            }

            if (bay.LongestActiveHours >= longOccupancyThreshold)
            {
                alerts.Add(new BayAlertItem
                {
                    Type = "OCCUPIED_TOO_LONG",
                    Severity = "HIGH",
                    BayId = bay.Id,
                    Code = bay.Code,
                    Name = bay.Name,
                    BranchName = bay.BranchName,
                    Area = bay.Area,
                    Message = $"Khoang đang bị chiếm lâu ({Math.Round(bay.LongestActiveHours, 2)}h).",
                    CreatedAt = now
                });
            }

            if (bay.OperationalStatus == BayAdminConstants.StatusUnderMaintenance && bay.OccupiedCount > 0)
            {
                alerts.Add(new BayAlertItem
                {
                    Type = "MAINTENANCE_HAS_VEHICLE",
                    Severity = "CRITICAL",
                    BayId = bay.Id,
                    Code = bay.Code,
                    Name = bay.Name,
                    BranchName = bay.BranchName,
                    Area = bay.Area,
                    Message = "Khoang đang bảo trì nhưng vẫn có xe.",
                    CreatedAt = now
                });
            }

            if (bay.IsLocked && bay.OccupiedCount > 0)
            {
                alerts.Add(new BayAlertItem
                {
                    Type = "LOCKED_HAS_VEHICLE",
                    Severity = "HIGH",
                    BayId = bay.Id,
                    Code = bay.Code,
                    Name = bay.Name,
                    BranchName = bay.BranchName,
                    Area = bay.Area,
                    Message = "Khoang đang khóa nhưng vẫn có xe được gán.",
                    CreatedAt = now
                });
            }

            if (bay.OccupiedCount == 0 && bay.LastUsedAt.HasValue && bay.LastUsedAt.Value < inactiveThreshold)
            {
                alerts.Add(new BayAlertItem
                {
                    Type = "INACTIVE_TOO_LONG",
                    Severity = "MEDIUM",
                    BayId = bay.Id,
                    Code = bay.Code,
                    Name = bay.Name,
                    BranchName = bay.BranchName,
                    Area = bay.Area,
                    Message = "Khoang không hoạt động trong thời gian dài.",
                    CreatedAt = now
                });
            }
        }

        var ordered = alerts
            .OrderByDescending(x => BayAdminSettingsHelper.AlertSeverityScore(x.Severity))
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        return BayAdminCommandResult.Ok(new
        {
            items = ordered,
            total = ordered.Count
        });
    }

    public async Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        CancellationToken ct = default)
    {
        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        var rows = await BayAdminRowBuilder.BuildBayRowsAsync(_dataService, _configStore, includeHidden: false, settings: settings, ct: ct);

        BayVehicleDescriptor? vehicle = null;
        var normalizedSegment = "";
        if (vehicleId.HasValue)
        {
            vehicle = await _dataService.LoadVehicleDescriptorAsync(vehicleId.Value, ct);
            if (vehicle == null)
            {
                return BayAdminCommandResult.BadRequest("Không tìm thấy xe để gợi ý khoang.");
            }
            normalizedSegment = BayAdminSettingsHelper.NormalizeSegment(vehicle.Segment);
        }

        var sourceBay = sourceBayId.HasValue ? rows.FirstOrDefault(x => x.Id == sourceBayId.Value) : null;
        var normalizedPreferredArea = string.IsNullOrWhiteSpace(preferredArea)
            ? (sourceBay?.Area ?? "")
            : preferredArea.Trim();
        var normalizedServiceStatus = BayAdminSettingsHelper.NormalizeForSearch(serviceStatus);

        var candidates = rows
            .Where(x =>
                !x.IsLocked &&
                x.OperationalStatus != BayAdminConstants.StatusUnderMaintenance &&
                x.OperationalStatus != BayAdminConstants.StatusBroken &&
                x.OccupiedCount < x.Capacity &&
                (!branchId.HasValue || x.BranchId == branchId.Value) &&
                (!vehicleId.HasValue || BayAdminSettingsHelper.IsSegmentCompatible(normalizedSegment, x.SupportedSegments)))
            .Select(x =>
            {
                var score = 0d;
                var reasons = new List<string>();

                if (branchId.HasValue && x.BranchId == branchId.Value)
                {
                    score += 30;
                    reasons.Add("Dung chi nhanh");
                }

                if (!string.IsNullOrWhiteSpace(normalizedPreferredArea) &&
                    string.Equals(BayAdminSettingsHelper.NormalizeForSearch(x.Area), BayAdminSettingsHelper.NormalizeForSearch(normalizedPreferredArea), StringComparison.Ordinal))
                {
                    score += 25;
                    reasons.Add("Dung khu vuc uu tien");
                }

                if (!string.IsNullOrWhiteSpace(normalizedServiceStatus) &&
                    BayAdminSettingsHelper.NormalizeForSearch(x.BayType).Contains(normalizedServiceStatus))
                {
                    score += 20;
                    reasons.Add("Phù hợp trạng thái sửa chữa");
                }

                var freeSlots = x.Capacity - x.OccupiedCount;
                score += Math.Max(0, freeSlots) * 4;
                reasons.Add($"Con {freeSlots} cho trong");

                if (sourceBay != null)
                {
                    var distance = BayAdminSettingsHelper.EstimateCodeDistance(sourceBay.Code, x.Code);
                    score += Math.Max(0, 15 - distance);
                    reasons.Add($"Gần khoang nguồn (độ lệch mã: {distance})");
                }

                if (x.UtilizationRate < 40)
                {
                    score += 8;
                    reasons.Add("Muc do su dung thap");
                }

                if (!string.IsNullOrWhiteSpace(normalizedSegment))
                {
                    reasons.Add("Tuong thich loai xe");
                }

                return new
                {
                    bay = x,
                    score = Math.Round(score, 2),
                    reasons
                };
            })
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.bay.SequenceOrder)
            .ThenBy(x => x.bay.Code)
            .Take(10)
            .Select(x => new
            {
                id = x.bay.Id,
                sequenceOrder = x.bay.SequenceOrder,
                code = x.bay.Code,
                name = x.bay.Name,
                branchId = x.bay.BranchId,
                branchName = x.bay.BranchName,
                area = x.bay.Area,
                bayType = x.bay.BayType,
                occupiedCount = x.bay.OccupiedCount,
                capacity = x.bay.Capacity,
                score = x.score,
                reasons = x.reasons
            })
            .ToList();

        return BayAdminCommandResult.Ok(new
        {
            vehicle = vehicleId.HasValue && vehicle != null
                ? new
                {
                    id = vehicle.Id,
                    licensePlate = vehicle.LicensePlate,
                    modelName = vehicle.ModelName,
                    segment = vehicle.Segment
                }
                : null,
            items = candidates
        });
    }
}
