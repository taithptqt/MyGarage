using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Common;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class VehicleAdminQueryService : IVehicleAdminQueryService
{
    private readonly IVehicleAdminDataService _dataService;
    private readonly IAppConfigFileStore _configStore;

    public VehicleAdminQueryService(IVehicleAdminDataService dataService, IAppConfigFileStore configStore)
    {
        _dataService = dataService;
        _configStore = configStore;
    }

    public async Task<object> GetLookupsAsync(CancellationToken ct = default)
    {
        var branchRows = await _dataService.GetLookupBranchesAsync(ct);
        var branches = branchRows.Select(b => new
        {
            id = b.Id,
            name = b.Name,
            status = b.Status
        }).ToList();

        var brandRows = await _dataService.GetLookupBrandsAsync(ct);
        var brands = brandRows.Select(b => new
        {
            id = b.Id,
            code = b.Code,
            name = b.Name
        }).ToList();

        var modelRows = await _dataService.GetLookupModelsAsync(ct);
        var models = modelRows.Select(m => new
        {
            id = m.Id,
            brandId = m.BrandId,
            brandName = m.BrandName,
            modelName = m.ModelName,
            segment = m.Segment
        }).ToList();

        var bayRows = await _dataService.GetLookupBaysAsync(ct);
        var bays = bayRows.Select(b => new
        {
            id = b.Id,
            code = b.Code,
            name = b.Name,
            branchId = b.BranchId,
            branchName = b.BranchName,
            status = b.Status,
            isLocked = b.IsLocked
        }).ToList();

        var staffRows = await _dataService.GetLookupStaffsAsync(ct);
        var staffs = staffRows.Select(s => new
        {
            id = s.Id,
            fullName = s.FullName,
            branchId = s.BranchId,
            branchName = s.BranchName,
            roleName = s.RoleName,
            status = s.Status
        }).ToList();

        var rows = await VehicleAdminRowBuilder.BuildVehicleRowsAsync(_dataService, _configStore, includeHidden: true, ct: ct);
        var colorOptions = rows
            .Select(x => x.Color)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var yearOptions = rows
            .Where(x => x.ManufactureYear.HasValue)
            .Select(x => x.ManufactureYear!.Value)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        return new
        {
            branches,
            brands,
            models,
            bays,
            staffs,
            colorOptions,
            yearOptions,
            statusOptions = VehicleAdminSupport.VehicleStatusOptions
        };
    }

    public async Task<object> GetListAsync(VehicleAdminListQuery request, CancellationToken ct = default)
    {
        var rows = await VehicleAdminRowBuilder.BuildVehicleRowsAsync(_dataService, _configStore, includeHidden: true, ct: ct);
        var query = rows.AsEnumerable();

        if (!request.IncludeHidden) query = query.Where(x => !x.IsHidden);
        if (request.BranchId.HasValue) query = query.Where(x => x.BranchId == request.BranchId.Value);
        if (request.BrandId.HasValue) query = query.Where(x => x.BrandId == request.BrandId.Value);
        if (request.ModelId.HasValue) query = query.Where(x => x.ModelId == request.ModelId.Value);
        if (!string.IsNullOrWhiteSpace(request.Color)) query = query.Where(x => VehicleAdminSupport.NormalizeForSearch(x.Color).Contains(VehicleAdminSupport.NormalizeForSearch(request.Color)));
        if (request.ManufactureYear.HasValue) query = query.Where(x => x.ManufactureYear == request.ManufactureYear);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.CurrentStatus == VehicleAdminSupport.NormalizeVehicleStatus(request.Status));
        if (request.BayId.HasValue) query = query.Where(x => x.CurrentBayId == request.BayId.Value);
        if (request.StaffId.HasValue) query = query.Where(x => x.CurrentStaffId == request.StaffId.Value);
        if (request.CheckInFrom.HasValue) query = query.Where(x => x.CheckInTime.HasValue && x.CheckInTime.Value >= request.CheckInFrom.Value);
        if (request.CheckInTo.HasValue) query = query.Where(x => x.CheckInTime.HasValue && x.CheckInTime.Value <= request.CheckInTo.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var normalizedSearch = VehicleAdminSupport.NormalizeForSearch(request.Search);
            query = query.Where(x =>
                VehicleAdminSupport.NormalizeForSearch(x.VehicleCode).Contains(normalizedSearch) ||
                VehicleAdminSupport.NormalizeForSearch(x.LicensePlate).Contains(normalizedSearch) ||
                VehicleAdminSupport.NormalizeForSearch(x.CustomerName).Contains(normalizedSearch) ||
                VehicleAdminSupport.NormalizeForSearch(x.CustomerPhone).Contains(normalizedSearch));
        }

        var normalizedSort = VehicleAdminSupport.NormalizeKey(request.SortBy);
        var direction = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        query = normalizedSort switch
        {
            "STATUS" => direction == "asc"
                ? query.OrderBy(x => x.CurrentStatus).ThenBy(x => x.StatusSince)
                : query.OrderByDescending(x => x.CurrentStatus).ThenByDescending(x => x.StatusSince),
            "BRAND" or "BRAND_MODEL" => direction == "asc"
                ? query.OrderBy(x => x.BrandName).ThenBy(x => x.ModelName).ThenBy(x => x.LicensePlate)
                : query.OrderByDescending(x => x.BrandName).ThenByDescending(x => x.ModelName).ThenByDescending(x => x.LicensePlate),
            "CODE" => direction == "asc"
                ? query.OrderBy(x => x.VehicleCode)
                : query.OrderByDescending(x => x.VehicleCode),
            _ => direction == "asc"
                ? query.OrderBy(x => x.CheckInTime ?? DateTime.MinValue).ThenBy(x => x.LicensePlate)
                : query.OrderByDescending(x => x.CheckInTime ?? DateTime.MinValue).ThenByDescending(x => x.LicensePlate)
        };

        var items = query.Select(VehicleAdminRowBuilder.MapVehicleListItem).ToList();
        return new
        {
            items,
            total = items.Count
        };
    }

    public async Task<object> GetSummaryAsync(long? branchId, CancellationToken ct = default)
    {
        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var rows = await VehicleAdminRowBuilder.BuildVehicleRowsAsync(_dataService, _configStore, includeHidden: false, settings: settings, ct: ct);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var totalVehicles = rows.Count;
        var inShopVehicles = rows.Count(x => VehicleAdminSupport.IsInShopStatus(x.CurrentStatus));
        var noBayCount = rows.Count(x => !x.CurrentBayId.HasValue);
        var noStaffCount = rows.Count(x => !x.CurrentStaffId.HasValue);

        var statusBreakdown = rows
            .GroupBy(x => x.CurrentStatus)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count(),
                rate = totalVehicles > 0 ? Math.Round(g.Count() * 100d / totalVehicles, 2) : 0d
            })
            .OrderByDescending(x => x.count)
            .ToList();

        var byBrand = rows
            .GroupBy(x => new { x.BrandId, x.BrandName })
            .Select(g => new
            {
                brandId = g.Key.BrandId,
                brandName = g.Key.BrandName,
                total = g.Count()
            })
            .OrderByDescending(x => x.total)
            .ThenBy(x => x.brandName)
            .ToList();

        var byModel = rows
            .GroupBy(x => new { x.ModelId, x.ModelName, x.BrandName })
            .Select(g => new
            {
                modelId = g.Key.ModelId,
                modelName = g.Key.ModelName,
                brandName = g.Key.BrandName,
                total = g.Count()
            })
            .OrderByDescending(x => x.total)
            .ThenBy(x => x.modelName)
            .ToList();

        var byBranch = rows
            .Where(x => x.BranchId.HasValue)
            .GroupBy(x => new { x.BranchId, x.BranchName })
            .Select(g => new
            {
                branchId = g.Key.BranchId,
                branchName = g.Key.BranchName,
                total = g.Count(),
                inShop = g.Count(x => VehicleAdminSupport.IsInShopStatus(x.CurrentStatus))
            })
            .OrderByDescending(x => x.total)
            .ThenBy(x => x.branchName)
            .ToList();

        var byStaff = rows
            .Where(x => x.CurrentStaffId.HasValue)
            .GroupBy(x => new { x.CurrentStaffId, x.CurrentStaffName })
            .Select(g => new
            {
                staffId = g.Key.CurrentStaffId,
                staffName = g.Key.CurrentStaffName,
                total = g.Count()
            })
            .OrderByDescending(x => x.total)
            .ThenBy(x => x.staffName)
            .ToList();

        var today = VietnamTime.Now.Date;
        var checkInByDay = Enumerable.Range(0, 14)
            .Select(offset => today.AddDays(-offset))
            .OrderBy(d => d)
            .Select(day => new
            {
                day,
                count = rows.Count(x => x.CheckInTime.HasValue && x.CheckInTime.Value.Date == day)
            })
            .ToList();

        var completedByDay = Enumerable.Range(0, 14)
            .Select(offset => today.AddDays(-offset))
            .OrderBy(d => d)
            .Select(day => new
            {
                day,
                count = settings.StatusHistory.Count(x =>
                    x.ChangedAt.Date == day &&
                    (x.NewStatus == VehicleAdminSupport.StatusCompleted || x.NewStatus == VehicleAdminSupport.StatusDelivered) &&
                    rows.Any(r => r.Id == x.VehicleId))
            })
            .ToList();

        var mostFrequentBrand = byBrand.FirstOrDefault();
        var mostFrequentModel = byModel.FirstOrDefault();
        var longestInShop = rows.Where(x => x.CheckInTime.HasValue).OrderByDescending(x => x.InShopHours).FirstOrDefault();

        return new
        {
            totalVehicles,
            inShopVehicles,
            noBayCount,
            noStaffCount,
            statusBreakdown,
            byBrand,
            byModel,
            byBranch,
            byStaff,
            checkInByDay,
            completedByDay,
            mostFrequentBrand,
            mostFrequentModel,
            longestInShop = longestInShop == null ? null : new
            {
                id = longestInShop.Id,
                vehicleCode = longestInShop.VehicleCode,
                licensePlate = longestInShop.LicensePlate,
                inShopHours = Math.Round(longestInShop.InShopHours, 2),
                branchName = longestInShop.BranchName
            }
        };
    }

    public async Task<object> GetAlertsAsync(long? branchId, CancellationToken ct = default)
    {
        var now = VietnamTime.Now;
        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var rows = await VehicleAdminRowBuilder.BuildVehicleRowsAsync(_dataService, _configStore, includeHidden: false, settings: settings, nowUtc: now, ct: ct);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var alerts = new List<VehicleAlertItem>();
        foreach (var row in rows)
        {
            if (row.InShopHours >= settings.AlertRules.InShopTooLongHours && VehicleAdminSupport.IsInShopStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("HIGH", "IN_SHOP_TOO_LONG", row, $"Xe nam xuong qua lau ({Math.Round(row.InShopHours, 1)}h).", now));
            }

            if (!row.CurrentBayId.HasValue && VehicleAdminSupport.IsBusyStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("MEDIUM", "NO_BAY_ASSIGNED", row, "Xe chua duoc gan khoang.", now));
            }

            if (!row.CurrentStaffId.HasValue && VehicleAdminSupport.IsBusyStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("MEDIUM", "NO_STAFF_ASSIGNED", row, "Xe chưa được phân công nhân sự.", now));
            }

            if (row.CurrentStatus == VehicleAdminSupport.StatusWaitingParts && row.StatusDurationHours >= settings.AlertRules.WaitingPartsTooLongHours)
            {
                alerts.Add(NewVehicleAlert("HIGH", "WAITING_PARTS_TOO_LONG", row, $"Xe cho phu tung qua lau ({Math.Round(row.StatusDurationHours, 1)}h).", now));
            }

            if (row.CurrentStatus == VehicleAdminSupport.StatusCompleted && row.StatusDurationHours >= settings.AlertRules.CompletedNotDeliveredHours)
            {
                alerts.Add(NewVehicleAlert("HIGH", "COMPLETED_NOT_DELIVERED", row, "Xe đã hoàn tất nhưng chưa giao.", now));
            }

            if (row.StatusDurationHours >= settings.AlertRules.StatusTooLongHours && VehicleAdminSupport.IsBusyStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("LOW", "STATUS_TOO_LONG", row, $"Xe o trang thai {row.CurrentStatus} qua lau.", now));
            }

            if (row.BayOccupiedHours >= settings.AlertRules.BayOccupiedTooLongHours && row.CurrentBayId.HasValue)
            {
                alerts.Add(NewVehicleAlert("MEDIUM", "BAY_OCCUPIED_TOO_LONG", row, $"Xe nam khoang qua lau ({Math.Round(row.BayOccupiedHours, 1)}h).", now));
            }

            if (string.IsNullOrWhiteSpace(row.LicensePlate) || string.IsNullOrWhiteSpace(row.CustomerPhone) || row.ModelId <= 0)
            {
                alerts.Add(NewVehicleAlert("LOW", "MISSING_DATA", row, "Xe đang thiếu dữ liệu cơ bản.", now));
            }
        }

        var workloadAlerts = rows
            .Where(x => x.CurrentStaffId.HasValue)
            .GroupBy(x => new { x.CurrentStaffId, x.CurrentStaffName })
            .Where(g => g.Count() > settings.AlertRules.MaxVehiclesPerStaff)
            .Select(g => new VehicleAlertItem
            {
                Severity = "HIGH",
                Type = "STAFF_OVERLOADED",
                VehicleId = 0,
                VehicleCode = "-",
                LicensePlate = "-",
                Message = $"Nhan su {g.Key.CurrentStaffName} dang xu ly {g.Count()} xe.",
                CreatedAt = now
            });
        alerts.AddRange(workloadAlerts);

        var ordered = alerts
            .OrderByDescending(x => AlertSeverityScore(x.Severity))
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        return new { items = ordered, total = ordered.Count };
    }

    public async Task<VehicleAdminCommandResult> GetVehicleDetailAsync(long id, int takeHistory, CancellationToken ct = default)
    {
        takeHistory = Math.Clamp(takeHistory, 20, 2000);
        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var baySettings = await VehicleAdminSupport.ReadBaySettingsAsync(_configStore, ct);
        var rows = await VehicleAdminRowBuilder.BuildVehicleRowsAsync(_dataService, _configStore, includeHidden: true, settings: settings, baySettings: baySettings, ct: ct);
        var row = rows.FirstOrDefault(x => x.Id == id);
        if (row == null) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var statusHistory = settings.StatusHistory
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.ChangedAt)
            .Take(takeHistory)
            .ToList();

        var assignmentHistory = settings.AssignmentHistory
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.AssignedAt)
            .Take(takeHistory)
            .ToList();

        var bayHistory = baySettings.Sessions
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.AssignedAt)
            .Take(takeHistory)
            .Select(x => new
            {
                x.SessionId,
                x.BayId,
                x.AssignedAt,
                x.ReleasedAt,
                durationHours = Math.Round(Math.Max(0, ((x.ReleasedAt ?? VietnamTime.Now) - x.AssignedAt).TotalHours), 2),
                x.ServiceStatus,
                x.Note,
                isActive = x.ReleasedAt == null
            })
            .ToList();

        var vehicleLogs = settings.VehicleAuditLogs
            .Where(x => x.VehicleId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(takeHistory)
            .ToList();

        return VehicleAdminCommandResult.Ok(new
        {
            vehicle = VehicleAdminRowBuilder.MapVehicleListItem(row),
            statusHistory,
            assignmentHistory,
            bayHistory,
            vehicleLogs
        });
    }

    public async Task<object> GetHistoryAsync(long? vehicleId, long? branchId, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 50, 5000);
        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var baySettings = await VehicleAdminSupport.ReadBaySettingsAsync(_configStore, ct);
        var rows = await VehicleAdminRowBuilder.BuildVehicleRowsAsync(_dataService, _configStore, includeHidden: true, settings: settings, baySettings: baySettings, ct: ct);

        var vehicleIds = rows
            .Where(x => (!vehicleId.HasValue || x.Id == vehicleId.Value) && (!branchId.HasValue || x.BranchId == branchId.Value))
            .Select(x => x.Id)
            .ToHashSet();

        var statusEvents = settings.StatusHistory
            .Where(x => vehicleIds.Contains(x.VehicleId))
            .Select(x => new VehicleTimelineItem
            {
                VehicleId = x.VehicleId,
                Type = "STATUS_CHANGE",
                Title = $"Status: {x.OldStatus} -> {x.NewStatus}",
                Description = x.Note,
                CreatedAt = x.ChangedAt,
                ActorId = x.ChangedBy,
                ActorName = x.ChangedByName
            });

        var assignmentEvents = settings.AssignmentHistory
            .Where(x => vehicleIds.Contains(x.VehicleId))
            .Select(x => new VehicleTimelineItem
            {
                VehicleId = x.VehicleId,
                Type = "STAFF_ASSIGN",
                Title = $"Staff: {x.OldStaffName ?? "(none)"} -> {x.NewStaffName ?? "(none)"}",
                Description = x.Note,
                CreatedAt = x.AssignedAt,
                ActorId = x.AssignedBy,
                ActorName = x.AssignedByName
            });

        var bayEvents = baySettings.Sessions
            .Where(x => vehicleIds.Contains(x.VehicleId))
            .Select(x => new VehicleTimelineItem
            {
                VehicleId = x.VehicleId,
                Type = x.ReleasedAt.HasValue ? "BAY_RELEASE" : "BAY_ASSIGN",
                Title = x.ReleasedAt.HasValue ? $"Release bay #{x.BayId}" : $"Assign bay #{x.BayId}",
                Description = x.Note,
                CreatedAt = x.ReleasedAt ?? x.AssignedAt
            });

        var adminEvents = settings.VehicleAuditLogs
            .Where(x => vehicleIds.Contains(x.VehicleId))
            .Select(x => new VehicleTimelineItem
            {
                VehicleId = x.VehicleId,
                Type = "ADMIN_ACTION",
                Title = x.Action,
                Description = x.Detail,
                CreatedAt = x.CreatedAt,
                ActorId = x.ActorId,
                ActorName = x.ActorName
            });

        var items = statusEvents
            .Concat(assignmentEvents)
            .Concat(bayEvents)
            .Concat(adminEvents)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToList();

        return new { items, total = items.Count };
    }

    private static VehicleAlertItem NewVehicleAlert(string severity, string type, VehicleViewRow row, string message, DateTime createdAt)
    {
        return new VehicleAlertItem
        {
            Severity = severity,
            Type = type,
            VehicleId = row.Id,
            VehicleCode = row.VehicleCode,
            LicensePlate = row.LicensePlate,
            Message = message,
            CreatedAt = createdAt
        };
    }

    private static int AlertSeverityScore(string? severity)
    {
        return VehicleAdminSupport.NormalizeKey(severity) switch
        {
            "CRITICAL" => 4,
            "HIGH" => 3,
            "MEDIUM" => 2,
            "LOW" => 1,
            _ => 0
        };
    }
}
