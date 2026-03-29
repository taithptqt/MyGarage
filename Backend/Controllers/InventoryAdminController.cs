using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;

namespace MyGarage.Controllers;

[Route("api/inventory-admin")]
[ApiController]
[Authorize(Roles = "Staff")]
public class InventoryAdminController : ControllerBase
{
    private readonly GarageContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public InventoryAdminController(GarageContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var now = DateTime.UtcNow;
        var staleCutoff = now.AddDays(-90);

        var inventoryRows = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
                .ThenInclude(w => w.Branch)
            .ToListAsync();

        var transactions = await _context.StockTransactions
            .AsNoTracking()
            .ToListAsync();

        var pendingApprovals = await _context.ApprovalRequests
            .AsNoTracking()
            .CountAsync(a => a.Status != null && a.Status.ToUpper() == "PENDING");

        var pendingAudits = await _context.InventoryAudits
            .AsNoTracking()
            .CountAsync(a => a.Status != null && (a.Status.ToUpper() == "DRAFT" || a.Status.ToUpper() == "PENDING"));

        var lastTransactionByProduct = transactions
            .Where(t => t.ProductId > 0)
            .GroupBy(t => t.ProductId)
            .ToDictionary(g => g.Key, g => g.Max(t => t.CreatedAt ?? DateTime.MinValue));

        var staleProducts = inventoryRows
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.ProductId)
            .Count(g => !lastTransactionByProduct.TryGetValue(g.Key, out var lastTx) || lastTx < staleCutoff);

        var totalValue = inventoryRows.Sum(i => i.TotalInventoryValue ?? (i.Quantity * (i.CurrentMacPrice ?? i.Product.CostPrice ?? 0)));

        return Ok(new
        {
            branchCount = inventoryRows.Select(i => i.Warehouse.BranchId).Distinct().Count(),
            warehouseCount = inventoryRows.Select(i => i.WarehouseId).Distinct().Count(),
            totalQuantity = inventoryRows.Sum(i => i.Quantity),
            reservedQuantity = inventoryRows.Sum(i => i.ReservedQuantity ?? 0),
            lowStockCount = inventoryRows.Count(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value),
            negativeStockCount = inventoryRows.Count(i => i.Quantity < 0),
            defectiveStockCount = inventoryRows.Count(i => i.Warehouse.Type != null && i.Warehouse.Type.ToUpper().Contains("DEFECTIVE")),
            pendingApprovals,
            pendingAudits,
            staleProducts,
            totalInventoryValue = totalValue
        });
    }

    [HttpGet("branch-summary")]
    public async Task<IActionResult> GetBranchSummary()
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(g => new
            {
                branchId = g.Id,
                branchName = g.Name,
                branchStatus = g.Status
            })
            .ToListAsync();

        var warehouseCounts = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsDeleted != true)
            .GroupBy(w => w.BranchId)
            .Select(g => new { branchId = g.Key, warehouseCount = g.Count() })
            .ToDictionaryAsync(x => x.branchId, x => x.warehouseCount);

        var inventoryAgg = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Warehouse)
            .GroupBy(i => i.Warehouse.BranchId)
            .Select(g => new
            {
                branchId = g.Key,
                totalQuantity = g.Sum(x => x.Quantity),
                reservedQuantity = g.Sum(x => x.ReservedQuantity ?? 0),
                totalValue = g.Sum(x => x.TotalInventoryValue ?? (x.Quantity * (x.CurrentMacPrice ?? 0))),
                lowStockCount = g.Count(x => x.MinStockLevel.HasValue && x.Quantity <= x.MinStockLevel.Value),
                negativeStockCount = g.Count(x => x.Quantity < 0)
            })
            .ToDictionaryAsync(x => x.branchId, x => x);

        var data = branches.Select(b =>
        {
            var hasAgg = inventoryAgg.TryGetValue(b.branchId, out var agg);
            return new
            {
                b.branchId,
                b.branchName,
                b.branchStatus,
                warehouseCount = warehouseCounts.GetValueOrDefault(b.branchId, 0),
                totalQuantity = hasAgg ? agg!.totalQuantity : 0m,
                reservedQuantity = hasAgg ? agg!.reservedQuantity : 0m,
                totalValue = hasAgg ? agg!.totalValue : 0m,
                lowStockCount = hasAgg ? agg!.lowStockCount : 0,
                negativeStockCount = hasAgg ? agg!.negativeStockCount : 0
            };
        });

        return Ok(data);
    }

    [HttpGet("warehouse-structure")]
    public async Task<IActionResult> GetWarehouseStructure()
    {
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Include(w => w.Branch)
            .Where(w => w.IsDeleted != true)
            .OrderBy(w => w.Branch.Name)
            .ThenBy(w => w.Name)
            .Select(w => new
            {
                id = w.Id,
                code = w.Code,
                name = w.Name,
                type = w.Type,
                branchId = w.BranchId,
                branchName = w.Branch.Name,
                isActive = w.IsActive,
                managerId = w.ManagerId
            })
            .ToListAsync();

        var bins = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.LocationBin != null && i.LocationBin != "")
            .GroupBy(i => new { i.WarehouseId, i.LocationBin })
            .Select(g => new { warehouseId = g.Key.WarehouseId, bin = g.Key.LocationBin!, skuCount = g.Select(x => x.ProductId).Distinct().Count() })
            .ToListAsync();

        return Ok(new { warehouses, bins });
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var now = DateTime.UtcNow;
        var lowStocks = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
                .ThenInclude(w => w.Branch)
            .Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value)
            .OrderBy(i => i.Quantity)
            .Take(50)
            .Select(i => new AlertItem
            {
                Type = "LOW_STOCK",
                Severity = "HIGH",
                Message = i.Product.Sku + " - " + i.Product.Name + " th?p hon Min.",
                BranchName = i.Warehouse.Branch.Name,
                WarehouseName = i.Warehouse.Name,
                ReferenceId = i.Id,
                ReferenceType = "Inventory",
                CreatedAt = now
            })
            .ToListAsync();

        var txWithoutDoc = await _context.StockTransactions
            .AsNoTracking()
            .Where(t => !t.RetailOrderId.HasValue && !t.RetailReturnId.HasValue && !t.MaterialRequestId.HasValue && !t.MaterialReturnId.HasValue && !t.PurchaseOrderId.HasValue)
            .OrderByDescending(t => t.CreatedAt)
            .Take(30)
            .Select(t => new AlertItem
            {
                Type = "TX_NO_SOURCE",
                Severity = "HIGH",
                Message = "Giao d?ch kho #" + t.Id + " không có ch?ng t? ngu?n.",
                BranchName = "",
                WarehouseName = "",
                ReferenceId = t.Id,
                ReferenceType = "StockTransaction",
                CreatedAt = t.CreatedAt ?? now
            })
            .ToListAsync();

        var alerts = lowStocks.Concat(txWithoutDoc).OrderByDescending(a => a.CreatedAt).ToList();
        return Ok(alerts);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] long? branchId,
        [FromQuery] long? warehouseId,
        [FromQuery] long? productId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _context.StockTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Include(t => t.CreatedByNavigation)
            .Join(_context.Warehouses.AsNoTracking(), t => t.WarehouseId, w => w.Id, (t, w) => new { t, w })
            .Join(_context.Branches.AsNoTracking(), tw => tw.w.BranchId, b => b.Id, (tw, b) => new { tw.t, tw.w, b })
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(x => x.b.Id == branchId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.w.Id == warehouseId.Value);
        if (productId.HasValue) query = query.Where(x => x.t.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalized = type.Trim().ToUpper();
            query = query.Where(x => x.t.Type != null && x.t.Type.ToUpper().Contains(normalized));
        }
        if (from.HasValue) query = query.Where(x => x.t.CreatedAt.HasValue && x.t.CreatedAt.Value >= from.Value);
        if (to.HasValue) query = query.Where(x => x.t.CreatedAt.HasValue && x.t.CreatedAt.Value <= to.Value);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(x => x.t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.t.Id,
                createdAt = x.t.CreatedAt,
                type = x.t.Type,
                quantity = x.t.Quantity,
                price = x.t.Price,
                note = x.t.Note,
                branchId = x.b.Id,
                branchName = x.b.Name,
                warehouseId = x.w.Id,
                warehouseName = x.w.Name,
                productId = x.t.ProductId,
                productSku = x.t.Product.Sku,
                productName = x.t.Product.Name,
                createdBy = x.t.CreatedBy,
                createdByName = x.t.CreatedByNavigation != null ? x.t.CreatedByNavigation.FullName : null,
                purchaseOrderId = x.t.PurchaseOrderId,
                retailOrderId = x.t.RetailOrderId,
                retailReturnId = x.t.RetailReturnId,
                materialRequestId = x.t.MaterialRequestId,
                materialReturnId = x.t.MaterialReturnId
            })
            .ToListAsync();

        var items = rows.Select(x => new
        {
            x.id,
            x.createdAt,
            x.type,
            x.quantity,
            x.price,
            x.note,
            x.branchId,
            x.branchName,
            x.warehouseId,
            x.warehouseName,
            x.productId,
            x.productSku,
            x.productName,
            x.createdBy,
            x.createdByName,
            source = ResolveSourceType(
                x.purchaseOrderId,
                x.retailOrderId,
                x.retailReturnId,
                x.materialRequestId,
                x.materialReturnId)
        });

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("approvals")]
    public async Task<IActionResult> GetApprovals([FromQuery] string? status)
    {
        var query = _context.ApprovalRequests
            .AsNoTracking()
            .Include(a => a.RequestedByNavigation)
            .Include(a => a.Approver)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpper();
            query = query.Where(a => a.Status != null && a.Status.ToUpper() == normalized);
        }

        var data = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                id = a.Id,
                requestType = a.RequestType,
                status = a.Status,
                reason = a.Reason,
                rejectReason = a.RejectReason,
                requestedBy = a.RequestedBy,
                requestedByName = a.RequestedByNavigation.FullName,
                approverId = a.ApproverId,
                approverName = a.Approver != null ? a.Approver.FullName : null,
                createdAt = a.CreatedAt,
                updatedAt = a.UpdatedAt
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost("approvals/{id}/approve")]
    public async Task<IActionResult> ApproveRequest(long id, [FromBody] ApprovalActionDto payload)
    {
        var request = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id);
        if (request == null) return NotFound();

        request.Status = "APPROVED";
        request.ApproverId = payload.ApproverId ?? ResolveCurrentUserId();
        request.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(payload.Note)) request.Reason = payload.Note.Trim();

        await _context.SaveChangesAsync();
        return Ok(new { message = "Approval request approved." });
    }

    [HttpPost("approvals/{id}/reject")]
    public async Task<IActionResult> RejectRequest(long id, [FromBody] ApprovalActionDto payload)
    {
        var request = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id);
        if (request == null) return NotFound();

        request.Status = "REJECTED";
        request.ApproverId = payload.ApproverId ?? ResolveCurrentUserId();
        request.RejectReason = payload.Note;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Approval request rejected." });
    }

    [HttpGet("audits")]
    public async Task<IActionResult> GetAudits([FromQuery] string? status)
    {
        var query = _context.InventoryAudits
            .AsNoTracking()
            .Include(a => a.Warehouse)
                .ThenInclude(w => w.Branch)
            .Include(a => a.InventoryAuditDetails)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpper();
            query = query.Where(a => a.Status.ToUpper() == normalized);
        }

        var data = await query
            .OrderByDescending(a => a.AuditDate)
            .Select(a => new
            {
                id = a.Id,
                status = a.Status,
                auditDate = a.AuditDate,
                warehouseId = a.WarehouseId,
                warehouseName = a.Warehouse.Name,
                branchName = a.Warehouse.Branch.Name,
                approvedBy = a.ApprovedBy,
                createdBy = a.CreatedBy,
                detailCount = a.InventoryAuditDetails.Count,
                differenceTotal = a.InventoryAuditDetails.Sum(d => Math.Abs(d.DifferenceQty ?? 0))
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost("audits/{id}/approve")]
    public async Task<IActionResult> ApproveAudit(long id, [FromBody] ApprovalActionDto payload)
    {
        var audit = await _context.InventoryAudits.FirstOrDefaultAsync(a => a.Id == id);
        if (audit == null) return NotFound();

        audit.Status = "APPROVED";
        audit.ApprovedBy = payload.ApproverId ?? ResolveCurrentUserId();
        audit.UpdatedAt = DateTime.UtcNow;
        audit.UpdatedBy = payload.ApproverId ?? ResolveCurrentUserId();
        await _context.SaveChangesAsync();

        return Ok(new { message = "Audit approved." });
    }

    [HttpPost("audits/{id}/reject")]
    public async Task<IActionResult> RejectAudit(long id, [FromBody] ApprovalActionDto payload)
    {
        var audit = await _context.InventoryAudits.FirstOrDefaultAsync(a => a.Id == id);
        if (audit == null) return NotFound();

        audit.Status = "REJECTED";
        audit.UpdatedAt = DateTime.UtcNow;
        audit.UpdatedBy = payload.ApproverId ?? ResolveCurrentUserId();
        await _context.SaveChangesAsync();

        return Ok(new { message = "Audit rejected." });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports()
    {
        var inventoryRows = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
                .ThenInclude(w => w.Branch)
            .ToListAsync();

        var transactions = await _context.StockTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .ToListAsync();

        var stockByBranch = inventoryRows
            .GroupBy(i => new { i.Warehouse.BranchId, i.Warehouse.Branch.Name })
            .Select(g => new
            {
                branchId = g.Key.BranchId,
                branchName = g.Key.Name,
                totalQuantity = g.Sum(x => x.Quantity),
                totalValue = g.Sum(x => x.TotalInventoryValue ?? (x.Quantity * (x.CurrentMacPrice ?? x.Product.CostPrice ?? 0)))
            })
            .OrderBy(x => x.branchName)
            .ToList();

        var topOutbound = transactions
            .Where(t => IsOutboundType(t.Type))
            .GroupBy(t => new { t.ProductId, t.Product.Sku, t.Product.Name })
            .Select(g => new
            {
                productId = g.Key.ProductId,
                sku = g.Key.Sku,
                productName = g.Key.Name,
                totalOutboundQty = g.Sum(x => Math.Abs(x.Quantity))
            })
            .OrderByDescending(x => x.totalOutboundQty)
            .Take(20)
            .ToList();

        var retailOrders = await _context.RetailOrders
            .AsNoTracking()
            .Include(r => r.Branch)
            .ToListAsync();

        var retailCompletedStatuses = new[] { "CONFIRMED", "SHIPPING", "DELIVERED", "COMPLETED" };
        var retailOrdersForRevenue = retailOrders
            .Where(r => r.Status != null && retailCompletedStatuses.Contains(r.Status.ToUpper()))
            .ToList();

        var retailRevenueTotal = retailOrdersForRevenue.Sum(r => r.FinalAmount);
        var retailOrderCount = retailOrders.Count;
        var retailAov = retailOrdersForRevenue.Count == 0 ? 0 : retailRevenueTotal / retailOrdersForRevenue.Count;

        var retailRevenueByBranch = retailOrdersForRevenue
            .GroupBy(r => new { r.BranchId, BranchName = r.Branch != null ? r.Branch.Name : "N/A" })
            .Select(g => new
            {
                branchId = g.Key.BranchId,
                branchName = g.Key.BranchName,
                revenue = g.Sum(x => x.FinalAmount),
                orderCount = g.Count()
            })
            .OrderByDescending(x => x.revenue)
            .ToList();

        var retailTopProducts = await _context.RetailOrderItems
            .AsNoTracking()
            .Include(x => x.Product)
            .GroupBy(x => new { x.ProductId, Sku = x.Product.Sku, Name = x.Product.Name })
            .Select(g => new
            {
                productId = g.Key.ProductId,
                sku = g.Key.Sku,
                productName = g.Key.Name,
                totalQty = g.Sum(x => x.Quantity),
                totalRevenue = g.Sum(x => x.FinalAmount)
            })
            .OrderByDescending(x => x.totalQty)
            .Take(20)
            .ToListAsync();

        return Ok(new
        {
            stockByBranch,
            nearOutOfStock = inventoryRows.Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel).Take(50),
            defectiveStocks = inventoryRows.Where(i => i.Warehouse.Type != null && i.Warehouse.Type.ToUpper().Contains("DEFECTIVE")),
            topOutbound,
            retailSummary = new
            {
                totalRevenue = retailRevenueTotal,
                orderCount = retailOrderCount,
                averageOrderValue = retailAov
            },
            retailRevenueByBranch,
            retailTopProducts
        });
    }

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetInventoryAuditLog([FromQuery] int take = 300)
    {
        take = Math.Clamp(take, 1, 2000);
        var inventoryTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Products",
            "ProductImages",
            "Inventories",
            "StockTransactions",
            "InventoryAudits",
            "InventoryAuditDetails",
            "Warehouses",
            "ApprovalRequests"
        };

        var data = await _context.AuditLogs
            .AsNoTracking()
            .Where(l => inventoryTables.Contains(l.TableName))
            .OrderByDescending(l => l.Timestamp)
            .Take(take)
            .Select(l => new
            {
                id = l.Id,
                timestamp = l.Timestamp,
                userId = l.UserId,
                userType = l.UserType,
                action = l.Action,
                tableName = l.TableName,
                recordId = l.RecordId,
                oldValue = l.OldValue,
                newValue = l.NewValue
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() => Ok(await ReadSettingsAsync());

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] InventoryAdminSettings settings)
    {
        settings ??= InventoryAdminSettings.CreateDefault();
        await WriteSettingsAsync(settings);
        return Ok(settings);
    }

    private bool IsOutboundType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        var normalized = type.Trim().ToUpperInvariant();
        return normalized.Contains("OUT") || normalized.Contains("ISSUE") || normalized.Contains("XUAT");
    }

    private static string ResolveSourceType(
        long? purchaseOrderId,
        long? retailOrderId,
        long? retailReturnId,
        long? materialRequestId,
        long? materialReturnId)
    {
        if (purchaseOrderId.HasValue) return "PurchaseOrder";
        if (retailOrderId.HasValue) return "RetailOrder";
        if (retailReturnId.HasValue) return "RetailReturn";
        if (materialRequestId.HasValue) return "MaterialRequest";
        if (materialReturnId.HasValue) return "MaterialReturn";
        return "Unknown";
    }

    private long? ResolveCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(claimValue, out var userId) ? userId : null;
    }

    private string SettingsPath
    {
        get
        {
            var folder = Path.Combine(_environment.ContentRootPath, "Configurations");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "inventory-admin-settings.json");
        }
    }

    private async Task<InventoryAdminSettings> ReadSettingsAsync()
    {
        if (!System.IO.File.Exists(SettingsPath)) return InventoryAdminSettings.CreateDefault();

        try
        {
            await using var stream = System.IO.File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<InventoryAdminSettings>(stream, _jsonOptions)
                ?? InventoryAdminSettings.CreateDefault();
        }
        catch
        {
            return InventoryAdminSettings.CreateDefault();
        }
    }

    private async Task WriteSettingsAsync(InventoryAdminSettings settings)
    {
        var sanitized = settings with { UpdatedAt = DateTime.UtcNow };
        await using var stream = System.IO.File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, sanitized, _jsonOptions);
    }

    public sealed record AlertItem
    {
        public string Type { get; init; } = "";
        public string Severity { get; init; } = "";
        public string Message { get; init; } = "";
        public string BranchName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public long ReferenceId { get; init; }
        public string ReferenceType { get; init; } = "";
        public DateTime CreatedAt { get; init; }
    }

    public sealed record ApprovalActionDto
    {
        public long? ApproverId { get; init; }
        public string? Note { get; init; }
    }

    public sealed record InventoryAdminSettings
    {
        public Dictionary<string, long> DefaultWarehouseByItemType { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> DefaultWarehouseByOperation { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<long, string> ProductClassifications { get; init; } = new();
        public List<string> UnitOptions { get; init; } = [];
        public InventoryPolicyRules PolicyRules { get; init; } = new();
        public DateTime? UpdatedAt { get; init; }

        public static InventoryAdminSettings CreateDefault() => new()
        {
            DefaultWarehouseByItemType = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["SELLABLE"] = 0,
                ["DEFECTIVE"] = 0,
                ["CONSUMABLE"] = 0,
                ["TRANSIT"] = 0
            },
            DefaultWarehouseByOperation = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["PURCHASE_RECEIVE"] = 0,
                ["SERVICE_ISSUE"] = 0,
                ["RETAIL_ISSUE"] = 0,
                ["MATERIAL_RETURN"] = 0,
                ["TRANSFER"] = 0
            },
            UnitOptions = ["Cai", "Bo", "Hop", "Lit", "Kg"],
            PolicyRules = new InventoryPolicyRules
            {
                AllowNegativeStock = false,
                LowStockThresholdPercent = 15,
                ReserveRule = "STRICT",
                AllowOverIssue = false,
                MaterialReturnRule = "ALLOW_WITH_REASON",
                TransferRule = "REQUIRE_APPROVAL"
            },
            UpdatedAt = DateTime.UtcNow
        };
    }

    public sealed record InventoryPolicyRules
    {
        public bool AllowNegativeStock { get; init; }
        public decimal LowStockThresholdPercent { get; init; }
        public string ReserveRule { get; init; } = "STRICT";
        public bool AllowOverIssue { get; init; }
        public string MaterialReturnRule { get; init; } = "ALLOW_WITH_REASON";
        public string TransferRule { get; init; } = "REQUIRE_APPROVAL";
        public Dictionary<long, decimal> SafeStockByBranch { get; init; } = new();
    }
}

