using System.Security.Claims;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers;

[Route("api/vehicle-admin")]
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
public class VehicleAdminController : ControllerBase
{
    private const string StatusWaitingReceive = "WAITING_RECEIVE";
    private const string StatusReceived = "RECEIVED";
    private const string StatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
    private const string StatusRepairing = "REPAIRING";
    private const string StatusInProgress = nameof(ServiceProgressStatus.IN_PROGRESS);
    private const string StatusWaitingParts = nameof(ServiceOrderDetailStatusCode.WAITING_PARTS);
    private const string StatusWaitingDelivery = "WAITING_DELIVERY";
    private const string StatusCompleted = nameof(ServiceOrderDetailStatusCode.COMPLETED);
    private const string StatusDelivered = nameof(RetailOrderStatus.DELIVERED);
    private const string StatusCanceled = nameof(ServiceOrderDetailStatusCode.CANCELED);

    private const string BayStatusEmpty = "EMPTY";
    private const string BayStatusInUse = "IN_USE";
    private const string BayStatusMaintenance = "MAINTENANCE";
    private const string BayStatusSuspended = "SUSPENDED";
    private const string DbBayStatusActive = nameof(BayOperationalStatusCode.ACTIVE);
    private const string DbBayStatusBroken = nameof(BayOperationalStatusCode.BROKEN);
    private const string DbBayStatusUnderMaintenance = nameof(BayOperationalStatusCode.UNDER_MAINTENANCE);

    private static readonly string[] VehicleStatusOptions =
    [
        StatusWaitingReceive,
        StatusReceived,
        StatusDiagnosing,
        StatusRepairing,
        StatusWaitingParts,
        StatusWaitingDelivery,
        StatusCompleted,
        StatusDelivered,
        StatusCanceled
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IGarageContext _context;
    private readonly IWebHostEnvironment _environment;

    public VehicleAdminController(IGarageContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet("lookups")]
    [HttpGet("~/api/admin/vehicles/lookups")]
    public async Task<IActionResult> GetLookups()
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                id = b.Id,
                name = b.Name,
                status = b.Status
            })
            .ToListAsync();

        var brands = await _context.VehicleBrands
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                id = b.Id,
                code = b.Code,
                name = b.Name
            })
            .ToListAsync();

        var models = await _context.VehicleModels
            .AsNoTracking()
            .Where(m => m.IsDeleted != true)
            .Include(m => m.Brand)
            .OrderBy(m => m.ModelName)
            .Select(m => new
            {
                id = m.Id,
                brandId = m.BrandId,
                brandName = m.Brand.Name,
                modelName = m.ModelName,
                segment = m.Segment
            })
            .ToListAsync();

        var bays = await _context.Bays
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .Include(b => b.Branch)
            .OrderBy(b => b.Code)
            .Select(b => new
            {
                id = b.Id,
                code = b.Code,
                name = b.Name,
                branchId = b.BranchId,
                branchName = b.Branch.Name,
                status = b.OperationalStatus,
                isLocked = b.IsActive == false
            })
            .ToListAsync();

        var staffs = await _context.Staffs
            .AsNoTracking()
            .Where(s => s.IsDeleted != true)
            .Include(s => s.Role)
            .Include(s => s.Branch)
            .OrderBy(s => s.FullName)
            .Select(s => new
            {
                id = s.Id,
                fullName = s.FullName,
                branchId = s.BranchId,
                branchName = s.Branch != null ? s.Branch.Name : null,
                roleName = s.Role != null ? s.Role.RoleName : null,
                status = s.Status
            })
            .ToListAsync();

        var rows = await BuildVehicleRowsAsync(includeHidden: true);
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

        return Ok(new
        {
            branches,
            brands,
            models,
            bays,
            staffs,
            colorOptions,
            yearOptions,
            statusOptions = VehicleStatusOptions
        });
    }

    [HttpGet("list")]
    [HttpGet("vehicles")]
    [HttpGet("~/api/admin/vehicles")]
    public async Task<IActionResult> GetList(
        [FromQuery] long? branchId,
        [FromQuery] long? brandId,
        [FromQuery] long? modelId,
        [FromQuery] string? color,
        [FromQuery] int? manufactureYear,
        [FromQuery] string? status,
        [FromQuery] long? bayId,
        [FromQuery] long? staffId,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
        [FromQuery] string? search,
        [FromQuery] string? sortBy = "checkin",
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] bool includeHidden = false)
    {
        var rows = await BuildVehicleRowsAsync(includeHidden: true);
        var query = rows.AsEnumerable();

        if (!includeHidden) query = query.Where(x => !x.IsHidden);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        if (brandId.HasValue) query = query.Where(x => x.BrandId == brandId.Value);
        if (modelId.HasValue) query = query.Where(x => x.ModelId == modelId.Value);
        if (!string.IsNullOrWhiteSpace(color)) query = query.Where(x => NormalizeForSearch(x.Color).Contains(NormalizeForSearch(color)));
        if (manufactureYear.HasValue) query = query.Where(x => x.ManufactureYear == manufactureYear);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.CurrentStatus == NormalizeVehicleStatus(status));
        if (bayId.HasValue) query = query.Where(x => x.CurrentBayId == bayId.Value);
        if (staffId.HasValue) query = query.Where(x => x.CurrentStaffId == staffId.Value);
        if (checkInFrom.HasValue) query = query.Where(x => x.CheckInTime.HasValue && x.CheckInTime.Value >= checkInFrom.Value);
        if (checkInTo.HasValue) query = query.Where(x => x.CheckInTime.HasValue && x.CheckInTime.Value <= checkInTo.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = NormalizeForSearch(search);
            query = query.Where(x =>
                NormalizeForSearch(x.VehicleCode).Contains(normalizedSearch) ||
                NormalizeForSearch(x.LicensePlate).Contains(normalizedSearch) ||
                NormalizeForSearch(x.CustomerName).Contains(normalizedSearch) ||
                NormalizeForSearch(x.CustomerPhone).Contains(normalizedSearch));
        }

        var normalizedSort = NormalizeKey(sortBy);
        var direction = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
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

        var items = query.Select(MapVehicleListItem).ToList();
        return Ok(new
        {
            items,
            total = items.Count
        });
    }

    [HttpGet("summary")]
    [HttpGet("~/api/admin/vehicles/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId)
    {
        var settings = await ReadSettingsAsync();
        var rows = await BuildVehicleRowsAsync(includeHidden: false, settings: settings);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var totalVehicles = rows.Count;
        var inShopVehicles = rows.Count(x => IsInShopStatus(x.CurrentStatus));
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
                inShop = g.Count(x => IsInShopStatus(x.CurrentStatus))
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
                    (x.NewStatus == StatusCompleted || x.NewStatus == StatusDelivered) &&
                    rows.Any(r => r.Id == x.VehicleId))
            })
            .ToList();

        var mostFrequentBrand = byBrand.FirstOrDefault();
        var mostFrequentModel = byModel.FirstOrDefault();
        var longestInShop = rows.Where(x => x.CheckInTime.HasValue).OrderByDescending(x => x.InShopHours).FirstOrDefault();

        return Ok(new
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
        });
    }

    [HttpGet("alerts")]
    [HttpGet("~/api/admin/vehicles/alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] long? branchId)
    {
        var now = VietnamTime.Now;
        var settings = await ReadSettingsAsync();
        var rows = await BuildVehicleRowsAsync(includeHidden: false, settings: settings, nowUtc: now);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var alerts = new List<VehicleAlertItem>();
        foreach (var row in rows)
        {
            if (row.InShopHours >= settings.AlertRules.InShopTooLongHours && IsInShopStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("HIGH", "IN_SHOP_TOO_LONG", row, $"Xe nam xuong qua lau ({Math.Round(row.InShopHours, 1)}h).", now));
            }

            if (!row.CurrentBayId.HasValue && IsBusyStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("MEDIUM", "NO_BAY_ASSIGNED", row, "Xe chua duoc gan khoang.", now));
            }

            if (!row.CurrentStaffId.HasValue && IsBusyStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("MEDIUM", "NO_STAFF_ASSIGNED", row, "Xe chua duoc phan cong nhan su.", now));
            }

            if (row.CurrentStatus == StatusWaitingParts && row.StatusDurationHours >= settings.AlertRules.WaitingPartsTooLongHours)
            {
                alerts.Add(NewVehicleAlert("HIGH", "WAITING_PARTS_TOO_LONG", row, $"Xe cho phu tung qua lau ({Math.Round(row.StatusDurationHours, 1)}h).", now));
            }

            if (row.CurrentStatus == StatusCompleted && row.StatusDurationHours >= settings.AlertRules.CompletedNotDeliveredHours)
            {
                alerts.Add(NewVehicleAlert("HIGH", "COMPLETED_NOT_DELIVERED", row, "Xe da hoan tat nhung chua giao.", now));
            }

            if (row.StatusDurationHours >= settings.AlertRules.StatusTooLongHours && IsBusyStatus(row.CurrentStatus))
            {
                alerts.Add(NewVehicleAlert("LOW", "STATUS_TOO_LONG", row, $"Xe o trang thai {row.CurrentStatus} qua lau.", now));
            }

            if (row.BayOccupiedHours >= settings.AlertRules.BayOccupiedTooLongHours && row.CurrentBayId.HasValue)
            {
                alerts.Add(NewVehicleAlert("MEDIUM", "BAY_OCCUPIED_TOO_LONG", row, $"Xe nam khoang qua lau ({Math.Round(row.BayOccupiedHours, 1)}h).", now));
            }

            if (string.IsNullOrWhiteSpace(row.LicensePlate) || string.IsNullOrWhiteSpace(row.CustomerPhone) || row.ModelId <= 0)
            {
                alerts.Add(NewVehicleAlert("LOW", "MISSING_DATA", row, "Xe dang thieu du lieu co ban.", now));
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

        return Ok(new { items = ordered, total = ordered.Count });
    }

    [HttpGet("vehicles/{id:long}")]
    [HttpGet("~/api/admin/vehicles/{id:long}")]
    public async Task<IActionResult> GetVehicleDetail(long id, [FromQuery] int takeHistory = 200)
    {
        takeHistory = Math.Clamp(takeHistory, 20, 2000);
        var settings = await ReadSettingsAsync();
        var baySettings = await ReadBaySettingsAsync();
        var rows = await BuildVehicleRowsAsync(includeHidden: true, settings: settings, baySettings: baySettings);
        var row = rows.FirstOrDefault(x => x.Id == id);
        if (row == null) return NotFound(new { message = "Vehicle not found." });

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

        return Ok(new
        {
            vehicle = MapVehicleListItem(row),
            statusHistory,
            assignmentHistory,
            bayHistory,
            vehicleLogs
        });
    }

    [HttpGet("history")]
    [HttpGet("~/api/admin/vehicles/history")]
    public async Task<IActionResult> GetHistory([FromQuery] long? vehicleId, [FromQuery] long? branchId, [FromQuery] int take = 300)
    {
        take = Math.Clamp(take, 50, 5000);
        var settings = await ReadSettingsAsync();
        var baySettings = await ReadBaySettingsAsync();
        var rows = await BuildVehicleRowsAsync(includeHidden: true, settings: settings, baySettings: baySettings);

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

        return Ok(new { items, total = items.Count });
    }

    [HttpPost("vehicles")]
    [HttpPost("~/api/admin/vehicles")]
    public async Task<IActionResult> CreateVehicle([FromBody] VehicleUpsertRequest request)
    {
        if (request == null) return BadRequest(new { message = "Payload is required." });
        if (request.ModelId <= 0) return BadRequest(new { message = "ModelId is required." });
        if (!request.ManufactureYear.HasValue || request.ManufactureYear.Value <= 0)
        {
            return BadRequest(new { message = "Vui lÃ²ng nháº­p nÄƒm sáº£n xuáº¥t." });
        }

        var modelExists = await _context.VehicleModels.AsNoTracking().AnyAsync(m => m.Id == request.ModelId && m.IsDeleted != true);
        if (!modelExists) return BadRequest(new { message = "Vehicle model does not exist." });

        var normalizedPlate = string.IsNullOrWhiteSpace(request.LicensePlate)
            ? $"TEMP-{Guid.NewGuid():N}".ToUpperInvariant()[..12]
            : request.LicensePlate.Trim().ToUpperInvariant();

        var duplicatedPlate = await _context.Vehicles.AsNoTracking()
            .AnyAsync(v => v.IsDeleted != true && v.LicensePlate == normalizedPlate);
        if (duplicatedPlate) return BadRequest(new { message = "License plate already exists." });

        if (!string.IsNullOrWhiteSpace(request.Vin))
        {
            var duplicatedVin = await _context.Vehicles.AsNoTracking()
                .AnyAsync(v => v.IsDeleted != true && v.Vin == request.Vin.Trim().ToUpperInvariant());
            if (duplicatedVin) return BadRequest(new { message = "VIN already exists." });
        }

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        var vehicle = new Vehicle
        {
            LicensePlate = normalizedPlate,
            Vin = string.IsNullOrWhiteSpace(request.Vin) ? null : request.Vin.Trim().ToUpperInvariant(),
            ModelId = request.ModelId,
            Color = NormalizeNullableText(request.Color),
            Odo = request.Odo,
            ManufactureYear = request.ManufactureYear,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, vehicle.Id);
        ApplyVehicleMetadata(metadata, request, now);
        if (request.BranchId.HasValue && request.BranchId.Value > 0) metadata.AssignedBranchId = request.BranchId.Value;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            UpdateVehicleStatus(metadata, settings, vehicle.Id, NormalizeVehicleStatus(request.Status), request.Note, userId, now, ResolveCurrentUserDisplayName());
        }
        if (request.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers.AsNoTracking().AnyAsync(c => c.Id == request.CustomerId.Value && c.IsDeleted != true);
            if (!customerExists) return BadRequest(new { message = "Customer does not exist." });
            await SetCurrentOwnerAsync(vehicle.Id, request.CustomerId, request.Note, now, userId);
        }

        AppendVehicleAudit(settings, vehicle.Id, "CREATE_VEHICLE", "Create vehicle by admin", userId, ResolveCurrentUserDisplayName(), now);
        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new
        {
            id = vehicle.Id,
            vehicleCode = FormatVehicleCode(vehicle.Id),
            message = "Vehicle created successfully."
        });
    }

    [HttpPut("vehicles/{id:long}")]
    [HttpPut("~/api/admin/vehicles/{id:long}")]
    public async Task<IActionResult> UpdateVehicle(long id, [FromBody] VehicleUpsertRequest request)
    {
        if (request == null) return BadRequest(new { message = "Payload is required." });
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, id);
        if (metadata.IsEditLocked) return BadRequest(new { message = "Vehicle is locked for editing." });

        if (request.ModelId > 0 && request.ModelId != vehicle.ModelId)
        {
            var modelExists = await _context.VehicleModels.AsNoTracking().AnyAsync(m => m.Id == request.ModelId && m.IsDeleted != true);
            if (!modelExists) return BadRequest(new { message = "Vehicle model does not exist." });
            vehicle.ModelId = request.ModelId;
        }

        if (!string.IsNullOrWhiteSpace(request.LicensePlate))
        {
            var normalizedPlate = request.LicensePlate.Trim().ToUpperInvariant();
            if (!string.Equals(vehicle.LicensePlate, normalizedPlate, StringComparison.OrdinalIgnoreCase))
            {
                var duplicatedPlate = await _context.Vehicles.AsNoTracking()
                    .AnyAsync(v => v.Id != id && v.IsDeleted != true && v.LicensePlate == normalizedPlate);
                if (duplicatedPlate) return BadRequest(new { message = "License plate already exists." });
                vehicle.LicensePlate = normalizedPlate;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Vin))
        {
            var normalizedVin = request.Vin.Trim().ToUpperInvariant();
            if (!string.Equals(vehicle.Vin, normalizedVin, StringComparison.OrdinalIgnoreCase))
            {
                var duplicatedVin = await _context.Vehicles.AsNoTracking()
                    .AnyAsync(v => v.Id != id && v.IsDeleted != true && v.Vin == normalizedVin);
                if (duplicatedVin) return BadRequest(new { message = "VIN already exists." });
                vehicle.Vin = normalizedVin;
            }
        }

        if (request.Color != null) vehicle.Color = NormalizeNullableText(request.Color);
        if (request.Odo.HasValue)
        {
            vehicle.Odo = request.Odo;
            vehicle.OdoLastUpdatedAt = VietnamTime.Now;
        }
        if (request.ManufactureYear.HasValue) vehicle.ManufactureYear = request.ManufactureYear;

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = userId;
        ApplyVehicleMetadata(metadata, request, now);

        if (request.BranchId.HasValue && request.BranchId.Value > 0) metadata.AssignedBranchId = request.BranchId.Value;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            UpdateVehicleStatus(metadata, settings, id, NormalizeVehicleStatus(request.Status), request.Note, userId, now, ResolveCurrentUserDisplayName());
        }
        if (request.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers.AsNoTracking().AnyAsync(c => c.Id == request.CustomerId.Value && c.IsDeleted != true);
            if (!customerExists) return BadRequest(new { message = "Customer does not exist." });
            await SetCurrentOwnerAsync(id, request.CustomerId, request.Note, now, userId);
        }

        AppendVehicleAudit(settings, id, "UPDATE_VEHICLE", "Update vehicle info", userId, ResolveCurrentUserDisplayName(), now);
        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle updated successfully." });
    }

    [HttpPost("vehicles/{id:long}/lock-edit")]
    [HttpPut("~/api/admin/vehicles/{id:long}/edit-locks")]
    public async Task<IActionResult> LockEdit(long id)
    {
        var exists = await _context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id);
        if (!exists) return NotFound(new { message = "Vehicle not found." });

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, id);
        metadata.IsEditLocked = true;
        metadata.UpdatedAt = VietnamTime.Now;

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        AppendVehicleAudit(settings, id, "LOCK_EDIT", "Lock edit vehicle", userId, ResolveCurrentUserDisplayName(), now);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle edit locked." });
    }

    [HttpPost("vehicles/{id:long}/unlock-edit")]
    [HttpDelete("~/api/admin/vehicles/{id:long}/edit-locks")]
    public async Task<IActionResult> UnlockEdit(long id)
    {
        var exists = await _context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id);
        if (!exists) return NotFound(new { message = "Vehicle not found." });

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, id);
        metadata.IsEditLocked = false;
        metadata.UpdatedAt = VietnamTime.Now;

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        AppendVehicleAudit(settings, id, "UNLOCK_EDIT", "Unlock edit vehicle", userId, ResolveCurrentUserDisplayName(), now);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle edit unlocked." });
    }

    [HttpPost("vehicles/{id:long}/hide")]
    [HttpDelete("~/api/admin/vehicles/{id:long}")]
    public async Task<IActionResult> HideVehicle(long id)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        vehicle.IsDeleted = true;
        vehicle.DeletedAt = now;
        vehicle.DeletedBy = userId;
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = userId;

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, id);
        metadata.IsHidden = true;
        metadata.UpdatedAt = now;
        AppendVehicleAudit(settings, id, "HIDE_VEHICLE", "Hide vehicle", userId, ResolveCurrentUserDisplayName(), now);

        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle hidden successfully." });
    }

    [HttpPost("vehicles/{id:long}/status")]
    [HttpPatch("~/api/admin/vehicles/{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] ChangeStatusRequest request)
    {
        var exists = await _context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id && v.IsDeleted != true);
        if (!exists) return NotFound(new { message = "Vehicle not found." });

        var normalizedStatus = NormalizeVehicleStatus(request.Status);
        if (!IsKnownVehicleStatus(normalizedStatus)) return BadRequest(new { message = "Invalid vehicle status." });

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, id);
        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        var actorName = ResolveCurrentUserDisplayName();

        UpdateVehicleStatus(metadata, settings, id, normalizedStatus, request.Note, userId, now, actorName);
        AppendVehicleAudit(settings, id, "SET_STATUS", $"Set status to {normalizedStatus}", userId, actorName, now);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle status updated." });
    }

    [HttpPost("vehicles/{id:long}/owner")]
    [HttpPatch("~/api/admin/vehicles/{id:long}/owner")]
    public async Task<IActionResult> ChangeOwner(long id, [FromBody] ChangeOwnerRequest request)
    {
        var exists = await _context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id && v.IsDeleted != true);
        if (!exists) return NotFound(new { message = "Vehicle not found." });

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers.AsNoTracking().AnyAsync(c => c.Id == request.CustomerId.Value && c.IsDeleted != true);
            if (!customerExists) return BadRequest(new { message = "Customer does not exist." });
        }

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        await SetCurrentOwnerAsync(id, request.CustomerId, request.Note, now, userId);

        var settings = await ReadSettingsAsync();
        AppendVehicleAudit(settings, id, "CHANGE_OWNER", request.CustomerId.HasValue ? $"Change owner to customer #{request.CustomerId.Value}" : "Clear owner", userId, ResolveCurrentUserDisplayName(), now);

        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle owner updated." });
    }

    [HttpPost("vehicles/{id:long}/assign-staff")]
    [HttpPost("~/api/admin/vehicles/{id:long}/staff-assignments")]
    public async Task<IActionResult> AssignStaff(long id, [FromBody] AssignStaffRequest request)
    {
        var exists = await _context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id && v.IsDeleted != true);
        if (!exists) return NotFound(new { message = "Vehicle not found." });

        Staff? staff = null;
        if (request.StaffId.HasValue)
        {
            staff = await _context.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StaffId.Value && s.IsDeleted != true);
            if (staff == null) return BadRequest(new { message = "Staff does not exist." });
        }

        var settings = await ReadSettingsAsync();
        var metadata = ResolveVehicleMetadata(settings, id);

        string? oldStaffName = null;
        if (metadata.CurrentStaffId.HasValue)
        {
            oldStaffName = await _context.Staffs.AsNoTracking()
                .Where(s => s.Id == metadata.CurrentStaffId.Value)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync();
        }

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        var actorName = ResolveCurrentUserDisplayName();

        settings.AssignmentHistory.Add(new VehicleAssignmentHistoryEntry
        {
            VehicleId = id,
            OldStaffId = metadata.CurrentStaffId,
            OldStaffName = oldStaffName,
            NewStaffId = staff?.Id,
            NewStaffName = staff?.FullName,
            TechnicalTeam = string.IsNullOrWhiteSpace(request.TechnicalTeam) ? metadata.TechnicalTeam : request.TechnicalTeam.Trim(),
            Note = request.Note?.Trim(),
            AssignedAt = now,
            AssignedBy = userId,
            AssignedByName = actorName
        });

        metadata.CurrentStaffId = staff?.Id;
        if (!string.IsNullOrWhiteSpace(request.TechnicalTeam)) metadata.TechnicalTeam = request.TechnicalTeam.Trim();
        metadata.UpdatedAt = now;

        AppendVehicleAudit(settings, id, "ASSIGN_STAFF", staff == null ? "Clear staff assignment" : $"Assign staff #{staff.Id}", userId, actorName, now);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle assignment updated." });
    }

    [HttpPost("vehicles/{id:long}/assign-bay")]
    [HttpPost("~/api/admin/vehicles/{id:long}/bay-assignments")]
    public async Task<IActionResult> AssignBay(long id, [FromBody] AssignBayRequest request)
    {
        if (request.BayId <= 0) return BadRequest(new { message = "BayId is required." });

        var vehicle = await _context.Vehicles.AsNoTracking()
            .Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });

        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == request.BayId && b.IsDeleted != true);
        if (bay == null) return BadRequest(new { message = "Bay does not exist." });

        var baySettings = await ReadBaySettingsAsync();
        var assignResult = await AssignVehicleToBayAsync(vehicle, bay, baySettings, request.ServiceStatus, request.Note);
        if (!assignResult.Success) return BadRequest(new { message = assignResult.Message });

        var now = VietnamTime.Now;
        var userId = ResolveCurrentUserId();
        var actorName = ResolveCurrentUserDisplayName();
        var settings = await ReadSettingsAsync();

        if (!string.IsNullOrWhiteSpace(request.NewVehicleStatus))
        {
            var normalizedStatus = NormalizeVehicleStatus(request.NewVehicleStatus);
            if (IsKnownVehicleStatus(normalizedStatus))
            {
                var metadata = ResolveVehicleMetadata(settings, id);
                UpdateVehicleStatus(metadata, settings, id, normalizedStatus, request.Note, userId, now, actorName);
                AppendVehicleAudit(settings, id, "SET_STATUS", $"Set status to {normalizedStatus} on bay assignment", userId, actorName, now);
            }
        }

        AppendVehicleAudit(settings, id, "ASSIGN_BAY", $"Assign to bay #{request.BayId}", userId, actorName, now);

        await _context.SaveChangesAsync();
        await WriteBaySettingsAsync(baySettings);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle assigned to bay." });
    }

    [HttpPost("vehicles/{id:long}/move-bay")]
    [HttpPost("~/api/admin/vehicles/{id:long}/bay-transfers")]
    public async Task<IActionResult> MoveBay(long id, [FromBody] MoveBayRequest request)
    {
        if (request.ToBayId <= 0) return BadRequest(new { message = "ToBayId is required." });

        var vehicle = await _context.Vehicles.AsNoTracking()
            .Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });

        var baySettings = await ReadBaySettingsAsync();
        var targetBay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == request.ToBayId && b.IsDeleted != true);
        if (targetBay == null) return BadRequest(new { message = "Target bay does not exist." });

        var activeSessions = baySettings.Sessions
            .Where(s => s.VehicleId == id && s.ReleasedAt == null)
            .ToList();

        var now = VietnamTime.Now;

        foreach (var session in activeSessions)
        {
            session.ReleasedAt = now;
            session.Note = MergeNote(session.Note, request.Note);

            var sourceBay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == session.BayId);
            if (sourceBay != null)
            {
                var remaining = baySettings.Sessions
                    .Where(s => s.BayId == sourceBay.Id && s.ReleasedAt == null && s.VehicleId != id)
                    .Select(s => s.VehicleId)
                    .FirstOrDefault();

                sourceBay.CurrentOccupiedVehicleId = remaining > 0 ? remaining : null;
                if (!sourceBay.CurrentOccupiedVehicleId.HasValue && NormalizeBayStatus(sourceBay.OperationalStatus) == BayStatusInUse)
                {
                    sourceBay.OperationalStatus = BayStatusEmpty;
                }
                sourceBay.UpdatedAt = now;
                sourceBay.UpdatedBy = ResolveCurrentUserId();
            }
        }

        var fallbackBays = await _context.Bays.Where(b => b.CurrentOccupiedVehicleId == id && b.IsDeleted != true).ToListAsync();
        foreach (var fb in fallbackBays)
        {
            fb.CurrentOccupiedVehicleId = null;
            if (NormalizeBayStatus(fb.OperationalStatus) == BayStatusInUse) fb.OperationalStatus = BayStatusEmpty;
            fb.UpdatedAt = now;
            fb.UpdatedBy = ResolveCurrentUserId();
        }

        var assignResult = await AssignVehicleToBayAsync(vehicle, targetBay, baySettings, request.ServiceStatus, request.Note);
        if (!assignResult.Success) return BadRequest(new { message = assignResult.Message });

        var userId = ResolveCurrentUserId();
        var actorName = ResolveCurrentUserDisplayName();
        var settings = await ReadSettingsAsync();

        if (!string.IsNullOrWhiteSpace(request.NewVehicleStatus))
        {
            var normalizedStatus = NormalizeVehicleStatus(request.NewVehicleStatus);
            if (IsKnownVehicleStatus(normalizedStatus))
            {
                var metadata = ResolveVehicleMetadata(settings, id);
                UpdateVehicleStatus(metadata, settings, id, normalizedStatus, request.Note, userId, now, actorName);
                AppendVehicleAudit(settings, id, "SET_STATUS", $"Set status to {normalizedStatus} on move bay", userId, actorName, now);
            }
        }

        AppendVehicleAudit(settings, id, "MOVE_BAY", $"Move vehicle to bay #{request.ToBayId}", userId, actorName, now);

        await _context.SaveChangesAsync();
        await WriteBaySettingsAsync(baySettings);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle moved to new bay." });
    }

    [HttpPost("vehicles/{id:long}/remove-bay")]
    [HttpPost("~/api/admin/vehicles/{id:long}/bay-removals")]
    public async Task<IActionResult> RemoveBay(long id, [FromBody] RemoveBayRequest request)
    {
        var exists = await _context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id && v.IsDeleted != true);
        if (!exists) return NotFound(new { message = "Vehicle not found." });

        var baySettings = await ReadBaySettingsAsync();
        var now = VietnamTime.Now;
        var activeSessions = baySettings.Sessions.Where(s => s.VehicleId == id && s.ReleasedAt == null).ToList();

        foreach (var session in activeSessions)
        {
            session.ReleasedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Note)) session.Note = MergeNote(session.Note, request.Note);

            var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == session.BayId);
            if (bay != null && bay.CurrentOccupiedVehicleId == id)
            {
                var remaining = baySettings.Sessions
                    .Where(s => s.BayId == bay.Id && s.ReleasedAt == null && s.VehicleId != id)
                    .Select(s => s.VehicleId)
                    .FirstOrDefault();

                bay.CurrentOccupiedVehicleId = remaining > 0 ? remaining : null;
                if (!bay.CurrentOccupiedVehicleId.HasValue && NormalizeBayStatus(bay.OperationalStatus) == BayStatusInUse)
                {
                    bay.OperationalStatus = BayStatusEmpty;
                }
                bay.UpdatedAt = now;
                bay.UpdatedBy = ResolveCurrentUserId();
            }
        }

        var fallbackBay = await _context.Bays.FirstOrDefaultAsync(b => b.CurrentOccupiedVehicleId == id && b.IsDeleted != true);
        if (fallbackBay != null)
        {
            fallbackBay.CurrentOccupiedVehicleId = null;
            if (NormalizeBayStatus(fallbackBay.OperationalStatus) == BayStatusInUse) fallbackBay.OperationalStatus = BayStatusEmpty;
            fallbackBay.UpdatedAt = now;
            fallbackBay.UpdatedBy = ResolveCurrentUserId();
        }

        var settings = await ReadSettingsAsync();
        AppendVehicleAudit(settings, id, "REMOVE_BAY", "Remove vehicle from bay", ResolveCurrentUserId(), ResolveCurrentUserDisplayName(), now);

        await _context.SaveChangesAsync();
        await WriteBaySettingsAsync(baySettings);
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle removed from bay." });
    }

    private async Task<AssignBayResult> AssignVehicleToBayAsync(
        Vehicle vehicle,
        Bay bay,
        BaySettingsSnapshot baySettings,
        string? serviceStatus,
        string? note)
    {
        var status = NormalizeBayStatus(bay.OperationalStatus);
        if (bay.IsDeleted == true) return AssignBayResult.Fail("Bay is hidden.");
        if (bay.IsActive == false) return AssignBayResult.Fail("Bay is locked.");
        if (status == BayStatusMaintenance) return AssignBayResult.Fail("Bay is under maintenance.");
        if (status == BayStatusSuspended) return AssignBayResult.Fail("Bay is suspended.");

        var metadata = ResolveBayMetadata(baySettings, bay.Id);
        var activeVehicleSet = baySettings.Sessions
            .Where(s => s.BayId == bay.Id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .ToHashSet();
        if (bay.CurrentOccupiedVehicleId.HasValue) activeVehicleSet.Add(bay.CurrentOccupiedVehicleId.Value);
        if (activeVehicleSet.Count >= metadata.Capacity) return AssignBayResult.Fail("Bay is full.");

        var activeAnywhere = baySettings.Sessions
            .Where(s => s.VehicleId == vehicle.Id && s.ReleasedAt == null)
            .OrderByDescending(s => s.AssignedAt)
            .FirstOrDefault();
        if (activeAnywhere != null) 
            return AssignBayResult.Fail(activeAnywhere.BayId == bay.Id ? "Vehicle is already assigned to this bay." : $"Vehicle is currently assigned in bay #{activeAnywhere.BayId}. Please move or remove it first.");

        var fallbackAnywhere = await _context.Bays.AsNoTracking()
            .Where(b => b.CurrentOccupiedVehicleId == vehicle.Id && b.IsDeleted != true)
            .Select(b => (long?)b.Id)
            .FirstOrDefaultAsync();
        if (fallbackAnywhere.HasValue) 
            return AssignBayResult.Fail(fallbackAnywhere.Value == bay.Id ? "Vehicle is already assigned to this bay." : $"Vehicle is currently assigned in bay #{fallbackAnywhere.Value}. Please move or remove it first.");

        if (!IsSegmentCompatible(NormalizeSegment(vehicle.Model?.Segment), metadata.SupportedSegments))
        {
            return AssignBayResult.Fail("Bay is not compatible with this vehicle segment.");
        }

        var now = VietnamTime.Now;
        baySettings.Sessions.Add(new BaySessionSnapshot
        {
            SessionId = Guid.NewGuid().ToString("N"),
            BayId = bay.Id,
            VehicleId = vehicle.Id,
            AssignedAt = now,
            ReleasedAt = null,
            ServiceStatus = NormalizeNullableText(serviceStatus),
            Note = NormalizeNullableText(note)
        });

        if (!bay.CurrentOccupiedVehicleId.HasValue) bay.CurrentOccupiedVehicleId = vehicle.Id;
        if (status == BayStatusEmpty) bay.OperationalStatus = BayStatusInUse;
        bay.UpdatedAt = now;
        bay.UpdatedBy = ResolveCurrentUserId();

        return AssignBayResult.Ok();
    }

    private async Task<List<VehicleViewRow>> BuildVehicleRowsAsync(
        bool includeHidden,
        VehicleAdminSettings? settings = null,
        BaySettingsSnapshot? baySettings = null,
        DateTime? nowUtc = null)
    {
        settings ??= await ReadSettingsAsync();
        baySettings ??= await ReadBaySettingsAsync();
        var now = nowUtc ?? VietnamTime.Now;

        var query = _context.Vehicles.AsNoTracking()
            .Include(v => v.Model)
            .ThenInclude(m => m.Brand)
            .AsQueryable();

        if (!includeHidden) query = query.Where(v => v.IsDeleted != true);
        var vehicles = await query.ToListAsync();
        if (vehicles.Count == 0) return new List<VehicleViewRow>();

        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var ownerLinks = await _context.CustomerVehicles.AsNoTracking()
            .Where(cv => vehicleIds.Contains(cv.VehicleId) && cv.IsDeleted != true)
            .Include(cv => cv.Customer)
            .ToListAsync();

        var ownerByVehicle = ownerLinks
            .GroupBy(cv => cv.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsCurrentOwner == true)
                    .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                    .First());

        var orders = await _context.ServiceOrders.AsNoTracking()
            .Where(o => vehicleIds.Contains(o.VehicleId))
            .Select(o => new ServiceOrderLite
            {
                VehicleId = o.VehicleId,
                ExecuteBranchId = o.ExecuteBranchId,
                Status = o.Status,
                CheckInTime = o.CheckInTime,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        var latestOrderByVehicle = orders
            .GroupBy(o => o.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CheckInTime ?? x.CreatedAt ?? DateTime.MinValue).First());

        var workSlots = await _context.WorkSlots.AsNoTracking()
            .Where(w => w.VehicleId.HasValue && vehicleIds.Contains(w.VehicleId.Value))
            .Include(w => w.Technician)
            .ToListAsync();

        var activeSlotByVehicle = workSlots
            .Where(w => w.VehicleId.HasValue && (w.ActualReleaseTime == null || NormalizeForSearch(w.Status) == StatusInProgress))
            .GroupBy(w => w.VehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ActualStartTime ?? x.StartTime).First());

        var activeSessionsByVehicle = baySettings.Sessions
            .Where(s => s.ReleasedAt == null && vehicleIds.Contains(s.VehicleId))
            .GroupBy(s => s.VehicleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First());

        var fallbackOccupiedBays = await _context.Bays.AsNoTracking()
            .Where(b => b.CurrentOccupiedVehicleId.HasValue && vehicleIds.Contains(b.CurrentOccupiedVehicleId.Value) && b.IsDeleted != true)
            .Include(b => b.Branch)
            .ToListAsync();

        var fallbackBayByVehicle = fallbackOccupiedBays
            .GroupBy(b => b.CurrentOccupiedVehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue).First());

        var bayIds = activeSessionsByVehicle.Values.Select(x => x.BayId)
            .Concat(fallbackOccupiedBays.Select(x => x.Id))
            .Distinct()
            .ToList();

        var bays = bayIds.Count == 0
            ? new List<Bay>()
            : await _context.Bays.AsNoTracking()
                .Where(b => bayIds.Contains(b.Id))
                .Include(b => b.Branch)
                .ToListAsync();
        var bayById = bays.ToDictionary(x => x.Id, x => x);

        var metadataStaffIds = settings.VehicleMetadata
            .Where(kvp => vehicleIds.Contains(kvp.Key) && kvp.Value?.CurrentStaffId.HasValue == true)
            .Select(kvp => kvp.Value!.CurrentStaffId!.Value)
            .Distinct()
            .ToList();

        var slotStaffIds = activeSlotByVehicle.Values
            .Where(x => x.TechnicianId.HasValue)
            .Select(x => x.TechnicianId!.Value)
            .Distinct()
            .ToList();

        var staffIds = metadataStaffIds.Concat(slotStaffIds).Distinct().ToList();
        var staffMap = staffIds.Count == 0
            ? new Dictionary<long, Staff>()
            : await _context.Staffs.AsNoTracking()
                .Where(s => staffIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s);

        var branchIds = settings.VehicleMetadata
            .Where(kvp => vehicleIds.Contains(kvp.Key) && kvp.Value?.AssignedBranchId.HasValue == true)
            .Select(kvp => kvp.Value!.AssignedBranchId!.Value)
            .Concat(latestOrderByVehicle.Values.Select(x => x.ExecuteBranchId))
            .Concat(bays.Select(b => b.BranchId))
            .Distinct()
            .ToList();

        var branchMap = branchIds.Count == 0
            ? new Dictionary<long, Branch>()
            : await _context.Branches.AsNoTracking()
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b);

        var rows = new List<VehicleViewRow>(vehicles.Count);
        foreach (var vehicle in vehicles)
        {
            var metadata = ResolveVehicleMetadata(settings, vehicle.Id);
            ownerByVehicle.TryGetValue(vehicle.Id, out var owner);
            latestOrderByVehicle.TryGetValue(vehicle.Id, out var latestOrder);
            activeSlotByVehicle.TryGetValue(vehicle.Id, out var activeSlot);

            Bay? currentBay = null;
            DateTime? bayAssignedAt = null;
            if (activeSessionsByVehicle.TryGetValue(vehicle.Id, out var activeSession))
            {
                bayAssignedAt = activeSession.AssignedAt;
                bayById.TryGetValue(activeSession.BayId, out currentBay);
            }
            else if (fallbackBayByVehicle.TryGetValue(vehicle.Id, out var fallbackBay))
            {
                currentBay = fallbackBay;
                bayAssignedAt = fallbackBay.UpdatedAt ?? fallbackBay.CreatedAt;
            }

            var status = NormalizeVehicleStatus(metadata.CurrentStatus);
            if (string.IsNullOrWhiteSpace(status)) status = NormalizeVehicleStatus(latestOrder?.Status);
            if (string.IsNullOrWhiteSpace(status)) status = StatusWaitingReceive;

            var statusSince = metadata.StatusUpdatedAt
                ?? latestOrder?.CheckInTime
                ?? latestOrder?.CreatedAt
                ?? vehicle.UpdatedAt
                ?? vehicle.CreatedAt
                ?? now;

            var branchId = metadata.AssignedBranchId
                ?? latestOrder?.ExecuteBranchId
                ?? currentBay?.BranchId;

            string? branchName = null;
            if (branchId.HasValue && branchMap.TryGetValue(branchId.Value, out var branch))
            {
                branchName = branch.Name;
            }

            var staffId = metadata.CurrentStaffId ?? activeSlot?.TechnicianId;
            string? staffName = null;
            if (staffId.HasValue && staffMap.TryGetValue(staffId.Value, out var staff))
            {
                staffName = staff.FullName;
            }

            var checkInTime = latestOrder?.CheckInTime ?? latestOrder?.CreatedAt;
            var isHidden = vehicle.IsDeleted == true || metadata.IsHidden;
            var inShopHours = checkInTime.HasValue ? Math.Max(0, (now - checkInTime.Value).TotalHours) : 0;
            var statusHours = Math.Max(0, (now - statusSince).TotalHours);
            var bayHours = bayAssignedAt.HasValue ? Math.Max(0, (now - bayAssignedAt.Value).TotalHours) : 0;

            rows.Add(new VehicleViewRow
            {
                Id = vehicle.Id,
                VehicleCode = FormatVehicleCode(vehicle.Id),
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                ModelId = vehicle.ModelId,
                ModelName = vehicle.Model?.ModelName ?? "-",
                BrandId = vehicle.Model?.BrandId ?? 0,
                BrandName = vehicle.Model?.Brand?.Name ?? "-",
                Segment = vehicle.Model?.Segment,
                Color = vehicle.Color,
                ManufactureYear = vehicle.ManufactureYear,
                Odo = vehicle.Odo,
                BranchId = branchId,
                BranchName = branchName,
                CheckInTime = checkInTime,
                CurrentStatus = status,
                StatusSince = statusSince,
                CurrentBayId = currentBay?.Id,
                CurrentBayCode = currentBay?.Code,
                CurrentBayName = currentBay?.Name,
                CurrentBayArea = currentBay == null ? null : ResolveBayMetadata(baySettings, currentBay.Id).Area,
                CurrentStaffId = staffId,
                CurrentStaffName = staffName,
                CustomerId = owner?.CustomerId,
                CustomerName = owner?.Customer?.FullName,
                CustomerPhone = owner?.Customer?.Phone,
                TechnicalTeam = metadata.TechnicalTeam,
                ExpectedArea = metadata.ExpectedArea,
                Version = metadata.Version,
                EngineNumber = metadata.EngineNumber,
                ChassisNumber = metadata.ChassisNumber,
                InitialCondition = metadata.InitialCondition,
                IsEditLocked = metadata.IsEditLocked,
                IsHidden = isHidden,
                InShopHours = inShopHours,
                StatusDurationHours = statusHours,
                BayOccupiedHours = bayHours,
                UpdatedAt = MaxDate(vehicle.UpdatedAt, metadata.UpdatedAt, now)
            });
        }

        return rows;
    }

    private static object MapVehicleListItem(VehicleViewRow row)
    {
        return new
        {
            id = row.Id,
            vehicleCode = row.VehicleCode,
            licensePlate = row.LicensePlate,
            vin = row.Vin,
            modelId = row.ModelId,
            modelName = row.ModelName,
            brandId = row.BrandId,
            brandName = row.BrandName,
            segment = row.Segment,
            color = row.Color,
            manufactureYear = row.ManufactureYear,
            odo = row.Odo,
            branchId = row.BranchId,
            branchName = row.BranchName,
            checkInTime = row.CheckInTime,
            currentStatus = row.CurrentStatus,
            statusSince = row.StatusSince,
            currentBayId = row.CurrentBayId,
            currentBayCode = row.CurrentBayCode,
            currentBayName = row.CurrentBayName,
            currentBayArea = row.CurrentBayArea,
            currentStaffId = row.CurrentStaffId,
            currentStaffName = row.CurrentStaffName,
            customerId = row.CustomerId,
            customerName = row.CustomerName,
            customerPhone = row.CustomerPhone,
            technicalTeam = row.TechnicalTeam,
            expectedArea = row.ExpectedArea,
            version = row.Version,
            engineNumber = row.EngineNumber,
            chassisNumber = row.ChassisNumber,
            initialCondition = row.InitialCondition,
            isEditLocked = row.IsEditLocked,
            isHidden = row.IsHidden,
            inShopHours = Math.Round(row.InShopHours, 2),
            statusDurationHours = Math.Round(row.StatusDurationHours, 2),
            bayOccupiedHours = Math.Round(row.BayOccupiedHours, 2),
            updatedAt = row.UpdatedAt
        };
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

    private static DateTime MaxDate(DateTime? first, DateTime? second, DateTime fallback)
    {
        var a = first ?? DateTime.MinValue;
        var b = second ?? DateTime.MinValue;
        var max = a > b ? a : b;
        return max == DateTime.MinValue ? fallback : max;
    }

    private static void ApplyVehicleMetadata(VehicleMetadata metadata, VehicleUpsertRequest request, DateTime now)
    {
        if (request.Version != null) metadata.Version = NormalizeNullableText(request.Version);
        if (request.EngineNumber != null) metadata.EngineNumber = NormalizeNullableText(request.EngineNumber);
        if (request.ChassisNumber != null) metadata.ChassisNumber = NormalizeNullableText(request.ChassisNumber);
        if (request.InitialCondition != null) metadata.InitialCondition = NormalizeNullableText(request.InitialCondition);
        if (request.TechnicalTeam != null) metadata.TechnicalTeam = NormalizeNullableText(request.TechnicalTeam);
        if (request.ExpectedArea != null) metadata.ExpectedArea = NormalizeNullableText(request.ExpectedArea);
        metadata.UpdatedAt = now;
    }

    private void UpdateVehicleStatus(
        VehicleMetadata metadata,
        VehicleAdminSettings settings,
        long vehicleId,
        string newStatus,
        string? note,
        long? userId,
        DateTime changedAt,
        string? actorName)
    {
        var oldStatus = NormalizeVehicleStatus(metadata.CurrentStatus);
        if (string.IsNullOrWhiteSpace(oldStatus)) oldStatus = StatusWaitingReceive;
        if (oldStatus == newStatus && metadata.StatusUpdatedAt.HasValue) return;

        metadata.CurrentStatus = newStatus;
        metadata.StatusUpdatedAt = changedAt;
        metadata.UpdatedAt = changedAt;

        settings.StatusHistory.Add(new VehicleStatusHistoryEntry
        {
            VehicleId = vehicleId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = NormalizeNullableText(note),
            ChangedAt = changedAt,
            ChangedBy = userId,
            ChangedByName = actorName
        });
    }

    private async Task SetCurrentOwnerAsync(long vehicleId, long? customerId, string? note, DateTime now, long? userId)
    {
        var links = await _context.CustomerVehicles
            .Where(cv => cv.VehicleId == vehicleId && cv.IsDeleted != true)
            .ToListAsync();

        var today = DateOnly.FromDateTime(now);
        foreach (var link in links.Where(x => x.IsCurrentOwner == true))
        {
            link.IsCurrentOwner = false;
            link.TransferDate = today;
            link.UpdatedAt = now;
            link.UpdatedBy = userId;
        }

        if (!customerId.HasValue) return;

        var customerExists = await _context.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId.Value && c.IsDeleted != true);
        if (!customerExists) throw new InvalidOperationException("Customer does not exist.");

        var target = links.FirstOrDefault(x => x.CustomerId == customerId.Value);
        if (target == null)
        {
            target = new CustomerVehicle
            {
                CustomerId = customerId.Value,
                VehicleId = vehicleId,
                IsCurrentOwner = true,
                OwnershipStartDate = today,
                TransferDate = null,
                Note = NormalizeNullableText(note),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
                IsDeleted = false
            };
            _context.CustomerVehicles.Add(target);
        }
        else
        {
            target.IsCurrentOwner = true;
            target.TransferDate = null;
            target.Note = MergeNote(target.Note, note);
            target.UpdatedAt = now;
            target.UpdatedBy = userId;
            if (!target.OwnershipStartDate.HasValue) target.OwnershipStartDate = today;
        }
    }

    private static string FormatVehicleCode(long vehicleId) => $"VEH-{vehicleId:000000}";

    private static void AppendVehicleAudit(
        VehicleAdminSettings settings,
        long vehicleId,
        string action,
        string? detail,
        long? actorId,
        string? actorName,
        DateTime createdAt)
    {
        settings.VehicleAuditLogs.Add(new VehicleAuditLogEntry
        {
            VehicleId = vehicleId,
            Action = NormalizeKey(action),
            Detail = NormalizeNullableText(detail),
            ActorId = actorId,
            ActorName = actorName,
            CreatedAt = createdAt
        });
    }

    private static bool IsKnownVehicleStatus(string status) => VehicleStatusOptions.Contains(status, StringComparer.OrdinalIgnoreCase);
    private static bool IsBusyStatus(string status) => status != StatusDelivered && status != StatusCanceled;
    private static bool IsInShopStatus(string status) => status != StatusDelivered && status != StatusCanceled;

    private static int AlertSeverityScore(string? severity)
    {
        return NormalizeKey(severity) switch
        {
            "CRITICAL" => 4,
            "HIGH" => 3,
            "MEDIUM" => 2,
            "LOW" => 1,
            _ => 0
        };
    }

    private string VehicleSettingsPath
    {
        get
        {
            var folder = Path.Combine(_environment.ContentRootPath, "Configurations");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "vehicle-admin-settings.json");
        }
    }

    private string BaySettingsPath
    {
        get
        {
            var folder = Path.Combine(_environment.ContentRootPath, "Configurations");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "bay-admin-settings.json");
        }
    }

    private async Task<VehicleAdminSettings> ReadSettingsAsync()
    {
        if (!System.IO.File.Exists(VehicleSettingsPath)) return VehicleAdminSettings.CreateDefault();

        try
        {
            await using var stream = System.IO.File.OpenRead(VehicleSettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<VehicleAdminSettings>(stream, JsonOptions)
                ?? VehicleAdminSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return VehicleAdminSettings.CreateDefault();
        }
    }

    private async Task WriteSettingsAsync(VehicleAdminSettings settings)
    {
        settings.Normalize();
        settings.UpdatedAt = VietnamTime.Now;

        await using var stream = System.IO.File.Create(VehicleSettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private async Task<BaySettingsSnapshot> ReadBaySettingsAsync()
    {
        if (!System.IO.File.Exists(BaySettingsPath)) return BaySettingsSnapshot.CreateDefault();

        try
        {
            await using var stream = System.IO.File.OpenRead(BaySettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<BaySettingsSnapshot>(stream, JsonOptions)
                ?? BaySettingsSnapshot.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return BaySettingsSnapshot.CreateDefault();
        }
    }

    private async Task WriteBaySettingsAsync(BaySettingsSnapshot settings)
    {
        settings.Normalize();
        await using var stream = System.IO.File.Create(BaySettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private static VehicleMetadata ResolveVehicleMetadata(VehicleAdminSettings settings, long vehicleId)
    {
        if (!settings.VehicleMetadata.TryGetValue(vehicleId, out var metadata) || metadata == null)
        {
            metadata = new VehicleMetadata();
            settings.VehicleMetadata[vehicleId] = metadata;
        }

        metadata.Normalize();
        return metadata;
    }

    private static BayMetadataSnapshot ResolveBayMetadata(BaySettingsSnapshot settings, long bayId)
    {
        if (!settings.BayMetadata.TryGetValue(bayId, out var metadata) || metadata == null)
        {
            metadata = BayMetadataSnapshot.CreateDefault();
            settings.BayMetadata[bayId] = metadata;
        }

        metadata.Normalize();
        return metadata;
    }

    private long? ResolveCurrentUserId()
    {
        return this.GetCurrentUserId();
    }

    private string? ResolveCurrentUserDisplayName()
    {
        return User.FindFirstValue(ClaimTypes.Name)
               ?? User.FindFirstValue("fullName")
               ?? User.FindFirstValue("username");
    }

    private static string NormalizeVehicleStatus(string? status)
    {
        var normalized = NormalizeKey(status);
        return normalized switch
        {
            "" => StatusWaitingReceive,
            nameof(ServiceOrderLifecycleStatus.NEW) or nameof(DiagnosisStatus.PENDING) or "WAITING" or "CHO_TIEP_NHAN" or "WAITING_RECEIVE" => StatusWaitingReceive,
            "RECEIVED" or "DA_TIEP_NHAN" => StatusReceived,
            nameof(ServiceOrderLifecycleStatus.DIAGNOSING) or "CHECKING" or "DANG_KIEM_TRA" => StatusDiagnosing,
            "REPAIRING" or StatusInProgress or "DANG_SUA" => StatusRepairing,
            nameof(ServiceOrderDetailStatusCode.WAITING_PARTS) or "CHO_PHU_TUNG" => StatusWaitingParts,
            "WAITING_DELIVERY" or "CHO_GIAO" => StatusWaitingDelivery,
            nameof(ServiceOrderDetailStatusCode.COMPLETED) or "DONE" or "HOAN_TAT" => StatusCompleted,
            nameof(RetailOrderStatus.DELIVERED) or "DA_GIAO" or "HANDOVER" => StatusDelivered,
            nameof(AppointmentStatus.CANCELED) or nameof(ServiceOrderDetailStatusCode.CANCELED) or "HUY" => StatusCanceled,
            _ => normalized
        };
    }

    private static string NormalizeBayStatus(string? status)
    {
        var normalized = NormalizeKey(status);
        return normalized switch
        {
            "" => BayStatusEmpty,
            DbBayStatusActive or "AVAILABLE" or "READY" or "EMPTY" or "FREE" or "TRONG" => BayStatusEmpty,
            "OCCUPIED" or "IN_USE" or "BUSY" or "USING" or "DANG_SU_DUNG" => BayStatusInUse,
            DbBayStatusUnderMaintenance or DbBayStatusBroken or "MAINTENANCE" or "REPAIR" or "BAO_TRI" => BayStatusMaintenance,
            "SUSPENDED" or "PAUSED" or "TEMP_STOP" or "TAM_NGUNG" => BayStatusSuspended,
            _ => normalized
        };
    }

    private static bool IsSegmentCompatible(string normalizedSegment, IReadOnlyList<string> supportedSegments)
    {
        if (supportedSegments.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(normalizedSegment)) return false;

        var normalizedList = supportedSegments
            .Select(NormalizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalizedList.Contains("ALL") || normalizedList.Contains(normalizedSegment);
    }

    private static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant();
    }

    private static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableText(string? value)
    {
        if (value == null) return null;
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string MergeNote(string? oldValue, string? newValue)
    {
        var oldText = string.IsNullOrWhiteSpace(oldValue) ? "" : oldValue.Trim();
        var newText = string.IsNullOrWhiteSpace(newValue) ? "" : newValue.Trim();
        if (string.IsNullOrWhiteSpace(newText)) return oldText;
        if (string.IsNullOrWhiteSpace(oldText)) return newText;
        return $"{oldText} | {newText}";
    }

    public sealed class VehicleUpsertRequest
    {
        [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
        public string? LicensePlate { get; set; }
        [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
        public string? Vin { get; set; }
        public long ModelId { get; set; }
        public string? Color { get; set; }
        public int? Odo { get; set; }
        [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
        public int? ManufactureYear { get; set; }
        public long? CustomerId { get; set; }
        public long? BranchId { get; set; }
        public string? Status { get; set; }
        public string? Version { get; set; }
        public string? EngineNumber { get; set; }
        public string? ChassisNumber { get; set; }
        public string? InitialCondition { get; set; }
        public string? TechnicalTeam { get; set; }
        public string? ExpectedArea { get; set; }
        public string? Note { get; set; }
    }

    public sealed class ChangeStatusRequest
    {
        public string? Status { get; set; }
        public string? Note { get; set; }
    }

    public sealed class ChangeOwnerRequest
    {
        public long? CustomerId { get; set; }
        public string? Note { get; set; }
    }

    public sealed class AssignBayRequest
    {
        public long BayId { get; set; }
        public string? ServiceStatus { get; set; }
        public string? Note { get; set; }
        public string? NewVehicleStatus { get; set; }
    }

    public sealed class MoveBayRequest
    {
        public long ToBayId { get; set; }
        public string? ServiceStatus { get; set; }
        public string? Note { get; set; }
        public string? NewVehicleStatus { get; set; }
    }

    public sealed class RemoveBayRequest
    {
        public string? Note { get; set; }
    }

    public sealed class AssignStaffRequest
    {
        public long? StaffId { get; set; }
        public string? TechnicalTeam { get; set; }
        public string? Note { get; set; }
    }

    public sealed class VehicleAdminSettings
    {
        public Dictionary<long, VehicleMetadata> VehicleMetadata { get; set; } = new();
        public List<VehicleStatusHistoryEntry> StatusHistory { get; set; } = [];
        public List<VehicleAssignmentHistoryEntry> AssignmentHistory { get; set; } = [];
        public List<VehicleAuditLogEntry> VehicleAuditLogs { get; set; } = [];
        public VehicleAlertRules AlertRules { get; set; } = new();
        public DateTime? UpdatedAt { get; set; }

        public static VehicleAdminSettings CreateDefault()
        {
            return new VehicleAdminSettings
            {
                AlertRules = new VehicleAlertRules
                {
                    InShopTooLongHours = 72,
                    StatusTooLongHours = 48,
                    WaitingPartsTooLongHours = 48,
                    CompletedNotDeliveredHours = 24,
                    BayOccupiedTooLongHours = 48,
                    MaxVehiclesPerStaff = 8
                },
                UpdatedAt = VietnamTime.Now
            };
        }

        public void Normalize()
        {
            VehicleMetadata ??= new Dictionary<long, VehicleMetadata>();
            StatusHistory ??= new List<VehicleStatusHistoryEntry>();
            AssignmentHistory ??= new List<VehicleAssignmentHistoryEntry>();
            VehicleAuditLogs ??= new List<VehicleAuditLogEntry>();
            AlertRules ??= new VehicleAlertRules();

            var normalizedMetadata = new Dictionary<long, VehicleMetadata>();
            foreach (var (key, value) in VehicleMetadata)
            {
                if (key <= 0) continue;
                var item = value ?? new VehicleMetadata();
                item.Normalize();
                normalizedMetadata[key] = item;
            }
            VehicleMetadata = normalizedMetadata;

            StatusHistory = StatusHistory
                .Where(x => x != null && x.VehicleId > 0 && x.ChangedAt != default)
                .OrderBy(x => x.ChangedAt)
                .Select(x =>
                {
                    x.OldStatus = NormalizeVehicleStatus(x.OldStatus);
                    x.NewStatus = NormalizeVehicleStatus(x.NewStatus);
                    x.Note = NormalizeNullableText(x.Note);
                    x.ChangedByName = NormalizeNullableText(x.ChangedByName);
                    return x;
                })
                .ToList();

            AssignmentHistory = AssignmentHistory
                .Where(x => x != null && x.VehicleId > 0 && x.AssignedAt != default)
                .OrderBy(x => x.AssignedAt)
                .Select(x =>
                {
                    x.Note = NormalizeNullableText(x.Note);
                    x.TechnicalTeam = NormalizeNullableText(x.TechnicalTeam);
                    x.AssignedByName = NormalizeNullableText(x.AssignedByName);
                    x.OldStaffName = NormalizeNullableText(x.OldStaffName);
                    x.NewStaffName = NormalizeNullableText(x.NewStaffName);
                    return x;
                })
                .ToList();

            VehicleAuditLogs = VehicleAuditLogs
                .Where(x => x != null && x.VehicleId > 0 && x.CreatedAt != default)
                .OrderBy(x => x.CreatedAt)
                .Select(x =>
                {
                    x.Action = NormalizeKey(x.Action);
                    x.Detail = NormalizeNullableText(x.Detail);
                    x.ActorName = NormalizeNullableText(x.ActorName);
                    return x;
                })
                .ToList();

            AlertRules.Normalize();
        }
    }

    public sealed class VehicleMetadata
    {
        public bool IsEditLocked { get; set; }
        public bool IsHidden { get; set; }
        public string CurrentStatus { get; set; } = StatusWaitingReceive;
        public DateTime? StatusUpdatedAt { get; set; }
        public long? AssignedBranchId { get; set; }
        public long? CurrentStaffId { get; set; }
        public string? TechnicalTeam { get; set; }
        public string? Version { get; set; }
        public string? EngineNumber { get; set; }
        public string? ChassisNumber { get; set; }
        public string? InitialCondition { get; set; }
        public string? ExpectedArea { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public void Normalize()
        {
            CurrentStatus = NormalizeVehicleStatus(CurrentStatus);
            TechnicalTeam = NormalizeNullableText(TechnicalTeam);
            Version = NormalizeNullableText(Version);
            EngineNumber = NormalizeNullableText(EngineNumber);
            ChassisNumber = NormalizeNullableText(ChassisNumber);
            InitialCondition = NormalizeNullableText(InitialCondition);
            ExpectedArea = NormalizeNullableText(ExpectedArea);
        }
    }

    public sealed class VehicleStatusHistoryEntry
    {
        public long VehicleId { get; set; }
        public string OldStatus { get; set; } = StatusWaitingReceive;
        public string NewStatus { get; set; } = StatusWaitingReceive;
        public string? Note { get; set; }
        public DateTime ChangedAt { get; set; }
        public long? ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
    }

    public sealed class VehicleAssignmentHistoryEntry
    {
        public long VehicleId { get; set; }
        public long? OldStaffId { get; set; }
        public string? OldStaffName { get; set; }
        public long? NewStaffId { get; set; }
        public string? NewStaffName { get; set; }
        public string? TechnicalTeam { get; set; }
        public string? Note { get; set; }
        public DateTime AssignedAt { get; set; }
        public long? AssignedBy { get; set; }
        public string? AssignedByName { get; set; }
    }

    public sealed class VehicleAuditLogEntry
    {
        public long VehicleId { get; set; }
        public string Action { get; set; } = "";
        public string? Detail { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? ActorId { get; set; }
        public string? ActorName { get; set; }
    }

    public sealed class VehicleAlertRules
    {
        public int InShopTooLongHours { get; set; } = 72;
        public int StatusTooLongHours { get; set; } = 48;
        public int WaitingPartsTooLongHours { get; set; } = 48;
        public int CompletedNotDeliveredHours { get; set; } = 24;
        public int BayOccupiedTooLongHours { get; set; } = 48;
        public int MaxVehiclesPerStaff { get; set; } = 8;

        public void Normalize()
        {
            InShopTooLongHours = Math.Clamp(InShopTooLongHours, 1, 24 * 180);
            StatusTooLongHours = Math.Clamp(StatusTooLongHours, 1, 24 * 180);
            WaitingPartsTooLongHours = Math.Clamp(WaitingPartsTooLongHours, 1, 24 * 180);
            CompletedNotDeliveredHours = Math.Clamp(CompletedNotDeliveredHours, 1, 24 * 180);
            BayOccupiedTooLongHours = Math.Clamp(BayOccupiedTooLongHours, 1, 24 * 180);
            MaxVehiclesPerStaff = Math.Clamp(MaxVehiclesPerStaff, 1, 100);
        }
    }

    public sealed class BaySettingsSnapshot
    {
        public Dictionary<long, BayMetadataSnapshot> BayMetadata { get; set; } = new();
        public List<BaySessionSnapshot> Sessions { get; set; } = [];

        public static BaySettingsSnapshot CreateDefault() => new();

        public void Normalize()
        {
            BayMetadata ??= new Dictionary<long, BayMetadataSnapshot>();
            Sessions ??= new List<BaySessionSnapshot>();

            var normalizedMetadata = new Dictionary<long, BayMetadataSnapshot>();
            foreach (var (key, value) in BayMetadata)
            {
                if (key <= 0) continue;
                var item = value ?? BayMetadataSnapshot.CreateDefault();
                item.Normalize();
                normalizedMetadata[key] = item;
            }
            BayMetadata = normalizedMetadata;

            Sessions = Sessions
                .Where(x => x != null && x.BayId > 0 && x.VehicleId > 0 && x.AssignedAt != default)
                .OrderBy(x => x.AssignedAt)
                .Select(x =>
                {
                    if (string.IsNullOrWhiteSpace(x.SessionId)) x.SessionId = Guid.NewGuid().ToString("N");
                    if (x.ReleasedAt.HasValue && x.ReleasedAt.Value < x.AssignedAt) x.ReleasedAt = x.AssignedAt;
                    x.ServiceStatus = NormalizeNullableText(x.ServiceStatus);
                    x.Note = NormalizeNullableText(x.Note);
                    return x;
                })
                .ToList();
        }
    }

    public sealed class BayMetadataSnapshot
    {
        public string Area { get; set; } = "Unassigned";
        public int Capacity { get; set; } = 1;
        public List<string> SupportedSegments { get; set; } = [];

        public static BayMetadataSnapshot CreateDefault() => new();

        public void Normalize()
        {
            Area = string.IsNullOrWhiteSpace(Area) ? "Unassigned" : Area.Trim();
            Capacity = Math.Clamp(Capacity, 1, 20);
            SupportedSegments = (SupportedSegments ?? [])
                .Select(NormalizeSegment)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public sealed class BaySessionSnapshot
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public long BayId { get; set; }
        public long VehicleId { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ServiceStatus { get; set; }
        public string? Note { get; set; }
    }

    private sealed class VehicleViewRow
    {
        public long Id { get; set; }
        public string VehicleCode { get; set; } = "";
        public string LicensePlate { get; set; } = "";
        public string? Vin { get; set; }
        public long ModelId { get; set; }
        public string ModelName { get; set; } = "";
        public long BrandId { get; set; }
        public string BrandName { get; set; } = "";
        public string? Segment { get; set; }
        public string? Color { get; set; }
        public int? ManufactureYear { get; set; }
        public int? Odo { get; set; }
        public long? BranchId { get; set; }
        public string? BranchName { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string CurrentStatus { get; set; } = StatusWaitingReceive;
        public DateTime StatusSince { get; set; }
        public long? CurrentBayId { get; set; }
        public string? CurrentBayCode { get; set; }
        public string? CurrentBayName { get; set; }
        public string? CurrentBayArea { get; set; }
        public long? CurrentStaffId { get; set; }
        public string? CurrentStaffName { get; set; }
        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? TechnicalTeam { get; set; }
        public string? ExpectedArea { get; set; }
        public string? Version { get; set; }
        public string? EngineNumber { get; set; }
        public string? ChassisNumber { get; set; }
        public string? InitialCondition { get; set; }
        public bool IsEditLocked { get; set; }
        public bool IsHidden { get; set; }
        public double InShopHours { get; set; }
        public double StatusDurationHours { get; set; }
        public double BayOccupiedHours { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class ServiceOrderLite
    {
        public long VehicleId { get; set; }
        public long ExecuteBranchId { get; set; }
        public string? Status { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    private sealed class VehicleAlertItem
    {
        public string Severity { get; set; } = "";
        public string Type { get; set; } = "";
        public long VehicleId { get; set; }
        public string VehicleCode { get; set; } = "";
        public string LicensePlate { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    private sealed class VehicleTimelineItem
    {
        public long VehicleId { get; set; }
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? ActorId { get; set; }
        public string? ActorName { get; set; }
    }

    private sealed class AssignBayResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";

        public static AssignBayResult Ok() => new() { Success = true, Message = "OK" };
        public static AssignBayResult Fail(string message) => new() { Success = false, Message = message };
    }
}


