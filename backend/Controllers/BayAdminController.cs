using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;

namespace MyGarage.Controllers;

[Route("api/bay-admin")]
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
public class BayAdminController : ControllerBase
{
    private const string StatusEmpty = "EMPTY";
    private const string StatusInUse = "IN_USE";
    private const string StatusMaintenance = "MAINTENANCE";
    private const string StatusSuspended = "SUSPENDED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly GarageContext _context;
    private readonly IWebHostEnvironment _environment;

    public BayAdminController(GarageContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups([FromQuery] int takeVehicles = 500)
    {
        takeVehicles = Math.Clamp(takeVehicles, 50, 5000);

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

        var vehicles = await _context.Vehicles
            .AsNoTracking()
            .Where(v => v.IsDeleted != true)
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .OrderByDescending(v => v.UpdatedAt ?? v.CreatedAt)
            .Take(takeVehicles)
            .Select(v => new
            {
                id = v.Id,
                licensePlate = v.LicensePlate,
                modelName = v.Model.ModelName,
                brandName = v.Model.Brand.Name,
                segment = v.Model.Segment
            })
            .ToListAsync();

        var settings = await ReadSettingsAsync();
        var areaOptions = settings.BayMetadata.Values
            .Select(m => NormalizeArea(m.Area))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a)
            .ToList();

        return Ok(new
        {
            branches,
            vehicles,
            areaOptions,
            statusOptions = new[]
            {
                StatusEmpty,
                StatusInUse,
                StatusMaintenance,
                StatusSuspended
            }
        });
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] long? branchId,
        [FromQuery] string? search,
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? sortBy = "code",
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] bool includeHidden = false)
    {
        var rows = await BuildBayRowsAsync(includeHidden: true);
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
            var normalized = NormalizeForSearch(search);
            query = query.Where(x =>
                NormalizeForSearch(x.Code).Contains(normalized) ||
                NormalizeForSearch(x.Name).Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            var normalizedArea = NormalizeForSearch(area);
            query = query.Where(x => NormalizeForSearch(x.Area).Contains(normalizedArea));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeOperationalStatus(status);
            query = query.Where(x => x.OperationalStatus == normalizedStatus);
        }

        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        var normalizedSort = NormalizeKey(sortBy);

        query = normalizedSort switch
        {
            "USAGE" => direction == "desc"
                ? query.OrderByDescending(x => x.SessionCount).ThenByDescending(x => x.TotalUsageHours).ThenBy(x => x.Code)
                : query.OrderBy(x => x.SessionCount).ThenBy(x => x.TotalUsageHours).ThenBy(x => x.Code),
            _ => direction == "desc"
                ? query.OrderByDescending(x => x.Code).ThenByDescending(x => x.Name)
                : query.OrderBy(x => x.Code).ThenBy(x => x.Name)
        };

        var items = query.Select(MapBayListItem).ToList();
        return Ok(new
        {
            items,
            total = items.Count
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId)
    {
        var rows = await BuildBayRowsAsync(includeHidden: true);
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

        return Ok(new
        {
            totalBays = visible.Count,
            hiddenBays = rows.Count(x => x.IsHidden),
            emptyBays = visible.Count(x => x.OperationalStatus == StatusEmpty),
            inUseBays = visible.Count(x => x.OperationalStatus == StatusInUse),
            maintenanceBays = visible.Count(x => x.OperationalStatus == StatusMaintenance),
            suspendedBays = visible.Count(x => x.OperationalStatus == StatusSuspended),
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

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage([FromQuery] long? branchId)
    {
        var rows = await BuildBayRowsAsync(includeHidden: false);
        if (branchId.HasValue)
        {
            rows = rows.Where(x => x.BranchId == branchId.Value).ToList();
        }

        var byBay = rows
            .OrderByDescending(x => x.SessionCount)
            .ThenByDescending(x => x.TotalUsageHours)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                id = x.Id,
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

        return Ok(new
        {
            byBay,
            mostUsed = byBay.Take(5),
            leastUsed = byBay.OrderBy(x => x.sessionCount).ThenBy(x => x.totalUsageHours).ThenBy(x => x.code).Take(5),
            frequentOverload = byBay.Where(x => x.occupiedCount > x.capacity).OrderByDescending(x => x.occupiedCount - x.capacity).Take(5),
            longestOccupied = byBay.Where(x => x.longestActiveHours > 0).OrderByDescending(x => x.longestActiveHours).Take(5)
        });
    }

    [HttpGet("layout")]
    public async Task<IActionResult> GetLayout([FromQuery] long? branchId)
    {
        var rows = await BuildBayRowsAsync(includeHidden: false);
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
                                .OrderBy(x => x.Code)
                                .Select(x => new
                                {
                                    id = x.Id,
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

        return Ok(new
        {
            branches
        });
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] long? branchId)
    {
        var now = DateTime.UtcNow;
        var settings = await ReadSettingsAsync();
        var rows = await BuildBayRowsAsync(includeHidden: false, settings: settings, nowUtc: now);
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
                        ? "Khoang vuot qua suc chua toi da."
                        : "Khoang da day.",
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
                    Message = $"Khoang dang bi chiem lau ({Math.Round(bay.LongestActiveHours, 2)}h).",
                    CreatedAt = now
                });
            }

            if (bay.OperationalStatus == StatusMaintenance && bay.OccupiedCount > 0)
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
                    Message = "Khoang dang bao tri nhung van co xe.",
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
                    Message = "Khoang dang khoa nhung van co xe duoc gan.",
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
                    Message = "Khoang khong hoat dong trong thoi gian dai.",
                    CreatedAt = now
                });
            }
        }

        var ordered = alerts
            .OrderByDescending(x => AlertSeverityScore(x.Severity))
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        return Ok(new
        {
            items = ordered,
            total = ordered.Count
        });
    }

    [HttpGet("bays/{id:long}/vehicles")]
    public async Task<IActionResult> GetBayVehicles(long id)
    {
        var settings = await ReadSettingsAsync();
        var rows = await BuildBayRowsAsync(includeHidden: true, settings: settings);
        var bay = rows.FirstOrDefault(x => x.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        return Ok(new
        {
            bay = MapBayListItem(bay),
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

    [HttpGet("bays/{id:long}/history")]
    public async Task<IActionResult> GetBayHistory(long id, [FromQuery] int take = 200)
    {
        take = Math.Clamp(take, 20, 2000);

        var bayExists = await _context.Bays.AsNoTracking().AnyAsync(b => b.Id == id);
        if (!bayExists) return NotFound(new { message = "Khoang khong ton tai." });

        var settings = await ReadSettingsAsync();
        var sessions = settings.Sessions
            .Where(s => s.BayId == id)
            .OrderByDescending(s => s.AssignedAt)
            .Take(take)
            .ToList();

        var vehicleMap = await LoadVehicleMapAsync(sessions.Select(s => s.VehicleId));
        var now = DateTime.UtcNow;

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

        return Ok(new
        {
            items,
            totalSessions = items.Count,
            uniqueVehicleCount = items.Select(x => x.vehicleId).Distinct().Count(),
            totalUsageHours = Math.Round(items.Sum(x => x.durationHours), 2)
        });
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] long? vehicleId,
        [FromQuery] long? branchId,
        [FromQuery] string? preferredArea,
        [FromQuery] long? sourceBayId,
        [FromQuery] string? serviceStatus)
    {
        var settings = await ReadSettingsAsync();
        var rows = await BuildBayRowsAsync(includeHidden: false, settings: settings);

        VehicleDescriptor? vehicle = null;
        var normalizedSegment = "";
        if (vehicleId.HasValue)
        {
            vehicle = await LoadVehicleDescriptorAsync(vehicleId.Value);
            if (vehicle == null)
            {
                return BadRequest(new { message = "Khong tim thay xe de goi y khoang." });
            }
            normalizedSegment = NormalizeSegment(vehicle.Segment);
        }

        var sourceBay = sourceBayId.HasValue ? rows.FirstOrDefault(x => x.Id == sourceBayId.Value) : null;
        var normalizedPreferredArea = string.IsNullOrWhiteSpace(preferredArea)
            ? (sourceBay?.Area ?? "")
            : preferredArea.Trim();
        var normalizedServiceStatus = NormalizeForSearch(serviceStatus);

        var candidates = rows
            .Where(x =>
                !x.IsLocked &&
                x.OperationalStatus != StatusMaintenance &&
                x.OperationalStatus != StatusSuspended &&
                x.OccupiedCount < x.Capacity &&
                (!branchId.HasValue || x.BranchId == branchId.Value) &&
                (!vehicleId.HasValue || IsSegmentCompatible(normalizedSegment, x.SupportedSegments)))
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
                    string.Equals(NormalizeForSearch(x.Area), NormalizeForSearch(normalizedPreferredArea), StringComparison.Ordinal))
                {
                    score += 25;
                    reasons.Add("Dung khu vuc uu tien");
                }

                if (!string.IsNullOrWhiteSpace(normalizedServiceStatus) &&
                    NormalizeForSearch(x.BayType).Contains(normalizedServiceStatus))
                {
                    score += 20;
                    reasons.Add("Phu hop trang thai sua chua");
                }

                var freeSlots = x.Capacity - x.OccupiedCount;
                score += Math.Max(0, freeSlots) * 4;
                reasons.Add($"Con {freeSlots} cho trong");

                if (sourceBay != null)
                {
                    var distance = EstimateCodeDistance(sourceBay.Code, x.Code);
                    score += Math.Max(0, 15 - distance);
                    reasons.Add($"Gan khoang nguon (do lech ma: {distance})");
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
            .ThenBy(x => x.bay.Code)
            .Take(10)
            .Select(x => new
            {
                id = x.bay.Id,
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

        return Ok(new
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

    [HttpPost("bays")]
    public async Task<IActionResult> CreateBay([FromBody] BayUpsertRequest request)
    {
        if (request == null) return BadRequest(new { message = "Payload is required." });
        if (request.BranchId <= 0) return BadRequest(new { message = "BranchId is required." });
        if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest(new { message = "Bay code is required." });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Bay name is required." });

        var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId && b.IsDeleted != true);
        if (!branchExists) return BadRequest(new { message = "Branch does not exist." });

        var normalizedCode = request.Code.Trim();
        var duplicatedCode = await _context.Bays
            .AsNoTracking()
            .AnyAsync(b => b.IsDeleted != true && b.Code == normalizedCode);
        if (duplicatedCode) return BadRequest(new { message = "Bay code already exists." });

        var now = DateTime.UtcNow;
        var userId = ResolveCurrentUserId();

        var bay = new Bay
        {
            BranchId = request.BranchId,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            BayType = string.IsNullOrWhiteSpace(request.BayType) ? "GENERAL" : request.BayType.Trim(),
            MaxTonnage = request.MaxTonnage,
            OperationalStatus = NormalizeOperationalStatus(request.OperationalStatus),
            IsActive = request.IsActive ?? true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        _context.Bays.Add(bay);
        await _context.SaveChangesAsync();

        var settings = await ReadSettingsAsync();
        settings.BayMetadata[bay.Id] = new BayMetadata
        {
            Area = NormalizeArea(request.Area),
            Capacity = NormalizeCapacity(request.Capacity),
            SupportedSegments = NormalizeSegments(request.SupportedSegments)
        };

        await WriteSettingsAsync(settings);

        return Ok(new
        {
            message = "Bay created successfully.",
            id = bay.Id
        });
    }

    [HttpPut("bays/{id:long}")]
    public async Task<IActionResult> UpdateBay(long id, [FromBody] BayUpsertRequest request)
    {
        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        if (request.BranchId <= 0) return BadRequest(new { message = "BranchId is required." });
        if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest(new { message = "Bay code is required." });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Bay name is required." });

        var duplicatedCode = await _context.Bays
            .AsNoTracking()
            .AnyAsync(b => b.Id != id && b.IsDeleted != true && b.Code == request.Code.Trim());
        if (duplicatedCode) return BadRequest(new { message = "Bay code already exists." });

        var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId && b.IsDeleted != true);
        if (!branchExists) return BadRequest(new { message = "Branch does not exist." });

        bay.BranchId = request.BranchId;
        bay.Code = request.Code.Trim();
        bay.Name = request.Name.Trim();
        bay.BayType = string.IsNullOrWhiteSpace(request.BayType) ? "GENERAL" : request.BayType.Trim();
        bay.MaxTonnage = request.MaxTonnage;
        bay.OperationalStatus = NormalizeOperationalStatus(request.OperationalStatus);
        bay.IsActive = request.IsActive ?? bay.IsActive;
        bay.UpdatedAt = DateTime.UtcNow;
        bay.UpdatedBy = ResolveCurrentUserId();

        var settings = await ReadSettingsAsync();
        settings.BayMetadata[id] = new BayMetadata
        {
            Area = NormalizeArea(request.Area),
            Capacity = NormalizeCapacity(request.Capacity),
            SupportedSegments = NormalizeSegments(request.SupportedSegments)
        };

        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Bay updated successfully." });
    }

    [HttpPost("bays/{id:long}/hide")]
    public async Task<IActionResult> HideBay(long id)
    {
        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        var userId = ResolveCurrentUserId();
        var now = DateTime.UtcNow;

        bay.IsDeleted = true;
        bay.IsActive = false;
        bay.DeletedAt = now;
        bay.DeletedBy = userId;
        bay.UpdatedAt = now;
        bay.UpdatedBy = userId;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Bay hidden successfully." });
    }

    [HttpPost("bays/{id:long}/lock")]
    public async Task<IActionResult> LockBay(long id)
    {
        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        bay.IsActive = false;
        bay.UpdatedAt = DateTime.UtcNow;
        bay.UpdatedBy = ResolveCurrentUserId();
        await _context.SaveChangesAsync();

        return Ok(new { message = "Bay locked successfully." });
    }

    [HttpPost("bays/{id:long}/unlock")]
    public async Task<IActionResult> UnlockBay(long id)
    {
        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        bay.IsActive = true;
        bay.UpdatedAt = DateTime.UtcNow;
        bay.UpdatedBy = ResolveCurrentUserId();
        await _context.SaveChangesAsync();

        return Ok(new { message = "Bay unlocked successfully." });
    }

    [HttpPost("bays/{id:long}/status")]
    public async Task<IActionResult> UpdateBayStatus(long id, [FromBody] UpdateBayStatusRequest request)
    {
        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        var normalizedStatus = NormalizeOperationalStatus(request.Status);
        if (!IsKnownOperationalStatus(normalizedStatus))
        {
            return BadRequest(new { message = "Trang thai khoang khong hop le." });
        }

        var settings = await ReadSettingsAsync();
        var activeSessions = settings.Sessions.Where(s => s.BayId == id && s.ReleasedAt == null).ToList();
        var activeVehicleCount = activeSessions.Select(s => s.VehicleId).Distinct().Count();
        if (bay.CurrentOccupiedVehicleId.HasValue)
        {
            activeVehicleCount = activeSessions.Select(s => s.VehicleId).Append(bay.CurrentOccupiedVehicleId.Value).Distinct().Count();
        }

        if (normalizedStatus == StatusEmpty && activeVehicleCount > 0)
        {
            return BadRequest(new { message = "Khoang van dang co xe, khong the chuyen ve TRONG." });
        }

        bay.OperationalStatus = normalizedStatus;
        bay.UpdatedAt = DateTime.UtcNow;
        bay.UpdatedBy = ResolveCurrentUserId();
        await _context.SaveChangesAsync();

        return Ok(new { message = "Bay status updated successfully." });
    }

    [HttpPost("bays/{id:long}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(long id, [FromBody] AssignVehicleRequest request)
    {
        if (request.VehicleId <= 0) return BadRequest(new { message = "VehicleId is required." });

        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        var vehicle = await LoadVehicleDescriptorAsync(request.VehicleId);
        if (vehicle == null) return BadRequest(new { message = "Khong tim thay xe can gan." });

        var settings = await ReadSettingsAsync();
        var metadata = ResolveMetadata(settings, bay.Id);
        var now = DateTime.UtcNow;

        var status = NormalizeOperationalStatus(bay.OperationalStatus);
        var invalidStateMessage = ValidateBayForAssign(bay, status);
        if (!string.IsNullOrWhiteSpace(invalidStateMessage))
        {
            return BadRequest(new { message = invalidStateMessage });
        }

        var activeSessionsInBay = settings.Sessions
            .Where(s => s.BayId == bay.Id && s.ReleasedAt == null)
            .ToList();

        var activeVehicleSet = activeSessionsInBay
            .Select(s => s.VehicleId)
            .ToHashSet();
        if (bay.CurrentOccupiedVehicleId.HasValue)
        {
            activeVehicleSet.Add(bay.CurrentOccupiedVehicleId.Value);
        }

        if (activeVehicleSet.Count >= metadata.Capacity)
        {
            return BadRequest(new { message = "Khoang da day, khong the gan them xe." });
        }

        var normalizedSegment = NormalizeSegment(vehicle.Segment);
        if (!IsSegmentCompatible(normalizedSegment, metadata.SupportedSegments))
        {
            return BadRequest(new { message = "Khoang khong phu hop voi loai xe nay." });
        }

        var activeBayId = await FindVehicleActiveBayAsync(request.VehicleId, settings, ignoreBayId: bay.Id);
        if (activeBayId.HasValue)
        {
            return BadRequest(new
            {
                message = $"Xe dang nam trong khoang #{activeBayId.Value}. Hay dieu phoi thay vi gan moi."
            });
        }

        settings.Sessions.Add(new BaySession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            BayId = bay.Id,
            VehicleId = request.VehicleId,
            AssignedAt = now,
            ReleasedAt = null,
            ServiceStatus = request.ServiceStatus?.Trim(),
            Note = request.Note?.Trim()
        });

        if (!bay.CurrentOccupiedVehicleId.HasValue)
        {
            bay.CurrentOccupiedVehicleId = request.VehicleId;
        }

        if (status == StatusEmpty)
        {
            bay.OperationalStatus = StatusInUse;
        }

        bay.UpdatedAt = now;
        bay.UpdatedBy = ResolveCurrentUserId();

        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle assigned to bay successfully." });
    }

    [HttpPost("bays/{id:long}/remove-vehicle")]
    public async Task<IActionResult> RemoveVehicleFromBay(long id, [FromBody] RemoveVehicleRequest request)
    {
        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id);
        if (bay == null) return NotFound(new { message = "Khoang khong ton tai." });

        var settings = await ReadSettingsAsync();
        var now = DateTime.UtcNow;

        var activeSessions = settings.Sessions
            .Where(s => s.BayId == id && s.ReleasedAt == null)
            .ToList();

        var targetSessions = request.VehicleId.HasValue
            ? activeSessions.Where(s => s.VehicleId == request.VehicleId.Value).ToList()
            : activeSessions;

        foreach (var session in targetSessions)
        {
            session.ReleasedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                session.Note = request.Note.Trim();
            }
        }

        var shouldReleaseCurrent = !request.VehicleId.HasValue ||
                                   (bay.CurrentOccupiedVehicleId.HasValue && bay.CurrentOccupiedVehicleId == request.VehicleId.Value);
        if (shouldReleaseCurrent && bay.CurrentOccupiedVehicleId.HasValue)
        {
            bay.CurrentOccupiedVehicleId = null;
        }

        var remainingVehicleIds = settings.Sessions
            .Where(s => s.BayId == id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .Distinct()
            .ToList();

        if (remainingVehicleIds.Count > 0)
        {
            bay.CurrentOccupiedVehicleId = remainingVehicleIds[0];
        }
        else
        {
            bay.CurrentOccupiedVehicleId = null;
            if (NormalizeOperationalStatus(bay.OperationalStatus) == StatusInUse)
            {
                bay.OperationalStatus = StatusEmpty;
            }
        }

        bay.UpdatedAt = now;
        bay.UpdatedBy = ResolveCurrentUserId();

        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle removed from bay successfully." });
    }

    [HttpPost("move-vehicle")]
    public async Task<IActionResult> MoveVehicle([FromBody] MoveVehicleRequest request)
    {
        if (request.VehicleId <= 0) return BadRequest(new { message = "VehicleId is required." });
        if (request.FromBayId <= 0 || request.ToBayId <= 0) return BadRequest(new { message = "FromBayId and ToBayId are required." });
        if (request.FromBayId == request.ToBayId) return BadRequest(new { message = "FromBayId and ToBayId must be different." });

        var bays = await _context.Bays
            .Where(b => b.Id == request.FromBayId || b.Id == request.ToBayId)
            .ToListAsync();

        var fromBay = bays.FirstOrDefault(b => b.Id == request.FromBayId);
        var toBay = bays.FirstOrDefault(b => b.Id == request.ToBayId);
        if (fromBay == null || toBay == null) return BadRequest(new { message = "Khoang nguon hoac dich khong ton tai." });

        var vehicle = await LoadVehicleDescriptorAsync(request.VehicleId);
        if (vehicle == null) return BadRequest(new { message = "Khong tim thay xe can dieu phoi." });

        var settings = await ReadSettingsAsync();
        var metadata = ResolveMetadata(settings, toBay.Id);
        var now = DateTime.UtcNow;

        var sourceSession = settings.Sessions
            .Where(s => s.BayId == fromBay.Id && s.VehicleId == request.VehicleId && s.ReleasedAt == null)
            .OrderByDescending(s => s.AssignedAt)
            .FirstOrDefault();

        if (sourceSession == null && fromBay.CurrentOccupiedVehicleId != request.VehicleId)
        {
            return BadRequest(new { message = "Xe hien khong nam trong khoang nguon." });
        }

        var targetStatus = NormalizeOperationalStatus(toBay.OperationalStatus);
        var invalidStateMessage = ValidateBayForAssign(toBay, targetStatus);
        if (!string.IsNullOrWhiteSpace(invalidStateMessage))
        {
            return BadRequest(new { message = invalidStateMessage });
        }

        var targetActiveVehicleSet = settings.Sessions
            .Where(s => s.BayId == toBay.Id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .ToHashSet();
        if (toBay.CurrentOccupiedVehicleId.HasValue)
        {
            targetActiveVehicleSet.Add(toBay.CurrentOccupiedVehicleId.Value);
        }

        if (targetActiveVehicleSet.Count >= metadata.Capacity)
        {
            return BadRequest(new { message = "Khoang dich da day." });
        }

        if (!IsSegmentCompatible(NormalizeSegment(vehicle.Segment), metadata.SupportedSegments))
        {
            return BadRequest(new { message = "Khoang dich khong phu hop loai xe." });
        }

        var activeElsewhere = await FindVehicleActiveBayAsync(request.VehicleId, settings, ignoreBayId: fromBay.Id);
        if (activeElsewhere.HasValue && activeElsewhere.Value != toBay.Id)
        {
            return BadRequest(new { message = $"Xe dang bi gan o khoang #{activeElsewhere.Value}." });
        }

        if (sourceSession != null)
        {
            sourceSession.ReleasedAt = now;
            sourceSession.Note = MergeNote(sourceSession.Note, request.Note);
        }
        else
        {
            settings.Sessions.Add(new BaySession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                BayId = fromBay.Id,
                VehicleId = request.VehicleId,
                AssignedAt = fromBay.UpdatedAt ?? now,
                ReleasedAt = now,
                ServiceStatus = request.ServiceStatus?.Trim(),
                Note = $"Auto close from currentOccupiedVehicleId. {request.Note}".Trim()
            });
        }

        settings.Sessions.Add(new BaySession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            BayId = toBay.Id,
            VehicleId = request.VehicleId,
            AssignedAt = now,
            ReleasedAt = null,
            ServiceStatus = request.ServiceStatus?.Trim(),
            Note = request.Note?.Trim()
        });

        var remainingInSource = settings.Sessions
            .Where(s => s.BayId == fromBay.Id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .Distinct()
            .ToList();

        fromBay.CurrentOccupiedVehicleId = remainingInSource.FirstOrDefault();
        if (fromBay.CurrentOccupiedVehicleId == 0) fromBay.CurrentOccupiedVehicleId = null;
        if (fromBay.CurrentOccupiedVehicleId == null && NormalizeOperationalStatus(fromBay.OperationalStatus) == StatusInUse)
        {
            fromBay.OperationalStatus = StatusEmpty;
        }

        if (!toBay.CurrentOccupiedVehicleId.HasValue)
        {
            toBay.CurrentOccupiedVehicleId = request.VehicleId;
        }
        if (targetStatus == StatusEmpty)
        {
            toBay.OperationalStatus = StatusInUse;
        }

        var userId = ResolveCurrentUserId();
        fromBay.UpdatedAt = now;
        fromBay.UpdatedBy = userId;
        toBay.UpdatedAt = now;
        toBay.UpdatedBy = userId;

        await _context.SaveChangesAsync();
        await WriteSettingsAsync(settings);

        return Ok(new { message = "Vehicle moved successfully." });
    }

    private async Task<List<BayViewRow>> BuildBayRowsAsync(
        bool includeHidden,
        BayAdminSettings? settings = null,
        DateTime? nowUtc = null)
    {
        settings ??= await ReadSettingsAsync();
        var now = nowUtc ?? DateTime.UtcNow;

        var bays = await _context.Bays
            .AsNoTracking()
            .Include(b => b.Branch)
            .Where(b => includeHidden || b.IsDeleted != true)
            .ToListAsync();

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

        var vehicleMap = await LoadVehicleMapAsync(activeVehicleIds);
        var rows = new List<BayViewRow>(bays.Count);

        foreach (var bay in bays)
        {
            usageMap.TryGetValue(bay.Id, out var usage);
            usage ??= new BayUsageMetrics();

            var metadata = ResolveMetadata(settings, bay.Id);
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

            var status = NormalizeOperationalStatus(bay.OperationalStatus);
            if (status == StatusEmpty && activeVehicles.Count > 0)
            {
                status = StatusInUse;
            }

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

    private async Task<Dictionary<long, VehicleDescriptor>> LoadVehicleMapAsync(IEnumerable<long> vehicleIds)
    {
        var ids = vehicleIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<long, VehicleDescriptor>();
        }

        var vehicles = await _context.Vehicles
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Select(v => new VehicleDescriptor
            {
                Id = v.Id,
                LicensePlate = v.LicensePlate,
                ModelName = v.Model.ModelName,
                BrandName = v.Model.Brand.Name,
                Segment = v.Model.Segment
            })
            .ToListAsync();

        return vehicles.ToDictionary(x => x.Id);
    }

    private async Task<VehicleDescriptor?> LoadVehicleDescriptorAsync(long vehicleId)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId && v.IsDeleted != true)
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Select(v => new VehicleDescriptor
            {
                Id = v.Id,
                LicensePlate = v.LicensePlate,
                ModelName = v.Model.ModelName,
                BrandName = v.Model.Brand.Name,
                Segment = v.Model.Segment
            })
            .FirstOrDefaultAsync();
    }

    private static Dictionary<long, BayUsageMetrics> BuildUsageMetrics(BayAdminSettings settings, DateTime nowUtc)
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

    private static object MapBayListItem(BayViewRow x)
    {
        return new
        {
            id = x.Id,
            branchId = x.BranchId,
            branchName = x.BranchName,
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

    private async Task<long?> FindVehicleActiveBayAsync(long vehicleId, BayAdminSettings settings, long? ignoreBayId = null)
    {
        var bySession = settings.Sessions
            .Where(s => s.VehicleId == vehicleId && s.ReleasedAt == null && (!ignoreBayId.HasValue || s.BayId != ignoreBayId.Value))
            .OrderByDescending(s => s.AssignedAt)
            .Select(s => (long?)s.BayId)
            .FirstOrDefault();

        if (bySession.HasValue) return bySession;

        return await _context.Bays
            .AsNoTracking()
            .Where(b =>
                b.IsDeleted != true &&
                b.CurrentOccupiedVehicleId == vehicleId &&
                (!ignoreBayId.HasValue || b.Id != ignoreBayId.Value))
            .Select(b => (long?)b.Id)
            .FirstOrDefaultAsync();
    }

    private static string ValidateBayForAssign(Bay bay, string normalizedStatus)
    {
        if (bay.IsDeleted == true) return "Khoang da bi an.";
        if (bay.IsActive == false) return "Khoang dang bi khoa.";
        if (normalizedStatus == StatusMaintenance) return "Khoang dang bao tri.";
        if (normalizedStatus == StatusSuspended) return "Khoang dang tam ngung.";
        return string.Empty;
    }

    private static string MergeNote(string? oldValue, string? newValue)
    {
        var oldText = string.IsNullOrWhiteSpace(oldValue) ? "" : oldValue.Trim();
        var newText = string.IsNullOrWhiteSpace(newValue) ? "" : newValue.Trim();
        if (string.IsNullOrWhiteSpace(newText)) return oldText;
        if (string.IsNullOrWhiteSpace(oldText)) return newText;
        return $"{oldText} | {newText}";
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

    private static int EstimateCodeDistance(string? sourceCode, string? targetCode)
    {
        var source = ExtractCodeNumber(sourceCode);
        var target = ExtractCodeNumber(targetCode);
        if (source.HasValue && target.HasValue)
        {
            return Math.Abs(source.Value - target.Value);
        }

        var a = string.IsNullOrWhiteSpace(sourceCode) ? "" : sourceCode.Trim().ToUpperInvariant();
        var b = string.IsNullOrWhiteSpace(targetCode) ? "" : targetCode.Trim().ToUpperInvariant();
        return LevenshteinDistance(a, b);
    }

    private static int? ExtractCodeNumber(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a == b) return 0;
        if (string.IsNullOrEmpty(a)) return b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var rows = a.Length + 1;
        var cols = b.Length + 1;
        var dp = new int[rows, cols];

        for (var i = 0; i < rows; i++) dp[i, 0] = i;
        for (var j = 0; j < cols; j++) dp[0, j] = j;

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[rows - 1, cols - 1];
    }

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

    private string SettingsPath
    {
        get
        {
            var folder = Path.Combine(_environment.ContentRootPath, "Configurations");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "bay-admin-settings.json");
        }
    }

    private async Task<BayAdminSettings> ReadSettingsAsync()
    {
        if (!System.IO.File.Exists(SettingsPath))
        {
            return BayAdminSettings.CreateDefault();
        }

        try
        {
            await using var stream = System.IO.File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<BayAdminSettings>(stream, JsonOptions)
                           ?? BayAdminSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return BayAdminSettings.CreateDefault();
        }
    }

    private async Task WriteSettingsAsync(BayAdminSettings settings)
    {
        settings.Normalize();
        settings.UpdatedAt = DateTime.UtcNow;

        await using var stream = System.IO.File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private static BayMetadata ResolveMetadata(BayAdminSettings settings, long bayId)
    {
        if (!settings.BayMetadata.TryGetValue(bayId, out var metadata) || metadata == null)
        {
            return BayMetadata.CreateDefault();
        }

        metadata.Normalize();
        return metadata;
    }

    private long? ResolveCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(claim, out var userId) ? userId : null;
    }

    private static string NormalizeOperationalStatus(string? status)
    {
        var normalized = NormalizeKey(status);
        return normalized switch
        {
            "" => StatusEmpty,
            "ACTIVE" or "AVAILABLE" or "READY" or "EMPTY" or "FREE" or "TRONG" => StatusEmpty,
            "OCCUPIED" or "IN_USE" or "BUSY" or "USING" or "DANG_SU_DUNG" => StatusInUse,
            "MAINTENANCE" or "REPAIR" or "BAO_TRI" => StatusMaintenance,
            "SUSPENDED" or "PAUSED" or "TEMP_STOP" or "TAM_NGUNG" => StatusSuspended,
            _ => normalized
        };
    }

    private static bool IsKnownOperationalStatus(string? normalizedStatus)
    {
        var value = NormalizeKey(normalizedStatus);
        return value == StatusEmpty || value == StatusInUse || value == StatusMaintenance || value == StatusSuspended;
    }

    private static string NormalizeArea(string? area)
    {
        if (string.IsNullOrWhiteSpace(area)) return "Chua phan khu";
        return area.Trim();
    }

    private static int NormalizeCapacity(int? capacity)
    {
        return Math.Clamp(capacity ?? 1, 1, 20);
    }

    private static List<string> NormalizeSegments(IEnumerable<string>? segments)
    {
        if (segments == null) return new List<string>();
        return segments
            .Select(NormalizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
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

    public sealed class BayUpsertRequest
    {
        public long BranchId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? BayType { get; set; }
        public decimal? MaxTonnage { get; set; }
        public string? OperationalStatus { get; set; }
        public bool? IsActive { get; set; }
        public string? Area { get; set; }
        public int? Capacity { get; set; }
        public List<string>? SupportedSegments { get; set; }
    }

    public sealed class UpdateBayStatusRequest
    {
        public string? Status { get; set; }
    }

    public sealed class AssignVehicleRequest
    {
        public long VehicleId { get; set; }
        public string? ServiceStatus { get; set; }
        public string? Note { get; set; }
    }

    public sealed class RemoveVehicleRequest
    {
        public long? VehicleId { get; set; }
        public string? Note { get; set; }
    }

    public sealed class MoveVehicleRequest
    {
        public long VehicleId { get; set; }
        public long FromBayId { get; set; }
        public long ToBayId { get; set; }
        public string? ServiceStatus { get; set; }
        public string? Note { get; set; }
    }

    public sealed class BayAdminSettings
    {
        public Dictionary<long, BayMetadata> BayMetadata { get; set; } = new();
        public List<BaySession> Sessions { get; set; } = [];
        public BayAlertRules AlertRules { get; set; } = new();
        public DateTime? UpdatedAt { get; set; }

        public static BayAdminSettings CreateDefault()
        {
            return new BayAdminSettings
            {
                AlertRules = new BayAlertRules
                {
                    LongOccupancyHours = 24,
                    InactiveDays = 30
                },
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Normalize()
        {
            BayMetadata ??= new Dictionary<long, BayMetadata>();
            Sessions ??= new List<BaySession>();
            AlertRules ??= new BayAlertRules();

            var normalizedMetadata = new Dictionary<long, BayMetadata>();
            foreach (var (key, value) in BayMetadata)
            {
                if (key <= 0) continue;
                var meta = value ?? BayAdminController.BayMetadata.CreateDefault();
                meta.Normalize();
                normalizedMetadata[key] = meta;
            }
            BayMetadata = normalizedMetadata;

            Sessions = Sessions
                .Where(s => s != null && s.BayId > 0 && s.VehicleId > 0 && s.AssignedAt != default)
                .OrderBy(s => s.AssignedAt)
                .Select(s =>
                {
                    if (string.IsNullOrWhiteSpace(s.SessionId))
                    {
                        s.SessionId = Guid.NewGuid().ToString("N");
                    }
                    if (s.ReleasedAt.HasValue && s.ReleasedAt.Value < s.AssignedAt)
                    {
                        s.ReleasedAt = s.AssignedAt;
                    }
                    return s;
                })
                .ToList();

            AlertRules.Normalize();
        }
    }

    public sealed class BayMetadata
    {
        public string Area { get; set; } = "Chua phan khu";
        public int Capacity { get; set; } = 1;
        public List<string> SupportedSegments { get; set; } = [];

        public static BayMetadata CreateDefault() => new();

        public void Normalize()
        {
            Area = NormalizeArea(Area);
            Capacity = NormalizeCapacity(Capacity);
            SupportedSegments = NormalizeSegments(SupportedSegments);
        }
    }

    public sealed class BaySession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public long BayId { get; set; }
        public long VehicleId { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ServiceStatus { get; set; }
        public string? Note { get; set; }
    }

    public sealed class BayAlertRules
    {
        public int LongOccupancyHours { get; set; } = 24;
        public int InactiveDays { get; set; } = 30;

        public void Normalize()
        {
            LongOccupancyHours = Math.Clamp(LongOccupancyHours, 1, 24 * 30);
            InactiveDays = Math.Clamp(InactiveDays, 1, 365);
        }
    }

    private sealed class BayAlertItem
    {
        public string Type { get; set; } = "";
        public string Severity { get; set; } = "";
        public long BayId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string BranchName { get; set; } = "";
        public string Area { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    private sealed class BayUsageMetrics
    {
        public int SessionCount { get; set; }
        public double TotalUsageHours { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public double LongestActiveHours { get; set; }
        public int VehicleHistoryCount { get; set; }
        public HashSet<long> VehicleIds { get; } = [];
        public List<BaySession> ActiveSessions { get; } = [];
    }

    private sealed class BayViewRow
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public string BranchName { get; set; } = "";
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string BayType { get; set; } = "";
        public string Area { get; set; } = "";
        public int Capacity { get; set; }
        public IReadOnlyList<string> SupportedSegments { get; set; } = [];
        public decimal? MaxTonnage { get; set; }
        public string OperationalStatus { get; set; } = StatusEmpty;
        public bool IsLocked { get; set; }
        public bool IsHidden { get; set; }
        public int OccupiedCount { get; set; }
        public double UtilizationRate { get; set; }
        public int SessionCount { get; set; }
        public double TotalUsageHours { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public double LongestActiveHours { get; set; }
        public int VehicleHistoryCount { get; set; }
        public IReadOnlyList<BayVehicleView> ActiveVehicles { get; set; } = [];
    }

    private sealed class BayVehicleView
    {
        public long VehicleId { get; set; }
        public string LicensePlate { get; set; } = "";
        public string? ModelName { get; set; }
        public string? BrandName { get; set; }
        public string? Segment { get; set; }
        public DateTime? AssignedAt { get; set; }
        public int OccupiedMinutes { get; set; }
    }

    private sealed class VehicleDescriptor
    {
        public long Id { get; set; }
        public string LicensePlate { get; set; } = "";
        public string? ModelName { get; set; }
        public string? BrandName { get; set; }
        public string? Segment { get; set; }
    }
}
