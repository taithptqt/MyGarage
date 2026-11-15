using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api/v1/inventory-management")]
[ApiController]
[Authorize]
public class InventoryAdminController : ControllerBase
{
    public sealed record DecisionPatchDto
    {
        public string? Decision { get; init; }
        public long? ApproverId { get; init; }
        public string? Note { get; init; }
    }

    private readonly IInventoryAdminService _inventoryAdminService;
    private readonly IInventoryAuthorizationService _inventoryAuthService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<InventoryAdminController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public InventoryAdminController(
        IInventoryAdminService inventoryAdminService,
        IInventoryAuthorizationService inventoryAuthService,
        IWebHostEnvironment environment,
        ILogger<InventoryAdminController> logger)
    {
        _inventoryAdminService = inventoryAdminService;
        _inventoryAuthService = inventoryAuthService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("dashboards/overview")]
    public async Task<IActionResult> GetOverview()
        => Ok(await _inventoryAdminService.GetOverviewAsync());

    [HttpGet("branch-summaries")]
    public async Task<IActionResult> GetBranchSummary()
        => Ok(await _inventoryAdminService.GetBranchSummaryAsync());

    [HttpGet("warehouse-structures")]
    public async Task<IActionResult> GetWarehouseStructure(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
        => Ok(await _inventoryAdminService.GetWarehouseStructureAsync(page, pageSize));

    [HttpGet("alert-items")]
    public async Task<IActionResult> GetAlerts()
    {
        var actor = this.ToUserActorContext();
        if (!await _inventoryAuthService.CanManageStockAlertsAsync(actor.BranchId, actor))
        {
            return Forbid();
        }

        return Ok(await _inventoryAdminService.GetStockAlertsAsync(false, 200));
    }

    [HttpGet("/api/v1/stock-alerts")]
    public async Task<IActionResult> GetStockAlerts(
        [FromQuery] bool? resolved = false,
        [FromQuery] int take = 200)
    {
        var actor = this.ToUserActorContext();
        if (!await _inventoryAuthService.CanManageStockAlertsAsync(actor.BranchId, actor))
        {
            return Forbid();
        }

        return Ok(await _inventoryAdminService.GetStockAlertsAsync(resolved, take));
    }

    [HttpPost("alert-items/rebuilds")]
    public async Task<IActionResult> RebuildAlerts()
    {
        var actor = this.ToUserActorContext();
        if (!await _inventoryAuthService.CanManageStockAlertsAsync(actor.BranchId, actor))
        {
            return Forbid();
        }

        var actorUserId = ResolveCurrentUserId();
        var rows = await _inventoryAdminService.RebuildStockAlertsAsync(actorUserId);
        return Ok(rows);
    }

    [HttpPost("alert-items/{id:long}/resolutions")]
    public async Task<IActionResult> ResolveAlert(long id, [FromBody] ResolveStockAlertRequestDto? payload)
    {
        if (payload == null || payload.PurchaseOrderId <= 0)
        {
            return this.BadRequestProblem("PurchaseOrderId is required.");
        }

        var actor = this.ToUserActorContext();
        if (!await _inventoryAuthService.CanManageStockAlertsAsync(actor.BranchId, actor))
        {
            return Forbid();
        }

        var ok = await _inventoryAdminService.ResolveStockAlertAsync(id, payload.PurchaseOrderId, ResolveCurrentUserId());
        if (!ok)
        {
            return this.NotFoundProblem("Alert not found or cannot resolve with the provided purchase order.");
        }

        return Ok(new { message = "Stock alert resolved." });
    }

    [HttpGet("stock-transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] long? branchId,
        [FromQuery] long? warehouseId,
        [FromQuery] long? productId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
        {
            return denied!;
        }

        var actor = this.ToUserActorContext();
        if (!await _inventoryAuthService.CanViewStockTransactionsAsync(scopedBranchId, actor))
        {
            return Forbid();
        }

        return Ok(await _inventoryAdminService.GetTransactionsAsync(scopedBranchId, warehouseId, productId, type, from, to, page, pageSize, search));
    }

    [HttpGet("/api/v1/inventory/history")]
    public async Task<IActionResult> GetInventoryHistory(
        [FromQuery] long? branchId,
        [FromQuery] long? warehouseId,
        [FromQuery] long? productId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
        {
            return denied!;
        }

        var actor = this.ToUserActorContext();
        if (!await _inventoryAuthService.CanViewStockTransactionsAsync(scopedBranchId, actor))
        {
            return Forbid();
        }

        return Ok(await _inventoryAdminService.GetTransactionsAsync(scopedBranchId, warehouseId, productId, type, from, to, page, pageSize, search));
    }

    [HttpGet("approval-requests")]
    public async Task<IActionResult> GetApprovals([FromQuery] string? status)
        => Ok(await _inventoryAdminService.GetApprovalsAsync(status));

    [HttpPatch("approval-requests/{id}")]
    public async Task<IActionResult> PatchApprovalDecision(long id, [FromBody] DecisionPatchDto payload)
    {
        var decision = (payload.Decision ?? string.Empty).Trim().ToUpperInvariant();
        var approverId = payload.ApproverId ?? ResolveCurrentUserId();

        var ok = decision switch
        {
            "APPROVE" => await _inventoryAdminService.ApproveRequestAsync(id, approverId, payload.Note),
            "REJECT" => await _inventoryAdminService.RejectRequestAsync(id, approverId, payload.Note),
            _ => false,
        };

        if (!ok)
        {
            if (decision is not "APPROVE" and not "REJECT")
            {
                return this.BadRequestProblem("Decision must be APPROVE or REJECT.");
            }

            return NotFound();
        }

        return Ok(new { message = decision == "APPROVE" ? "Approval request approved." : "Approval request rejected." });
    }

    [HttpGet("audit-requests")]
    public async Task<IActionResult> GetAudits([FromQuery] string? status)
        => Ok(await _inventoryAdminService.GetAuditsAsync(status));

    [HttpPatch("audit-requests/{id}")]
    public async Task<IActionResult> PatchAuditDecision(long id, [FromBody] DecisionPatchDto payload)
    {
        var decision = (payload.Decision ?? string.Empty).Trim().ToUpperInvariant();
        var approverId = payload.ApproverId ?? ResolveCurrentUserId();

        var ok = decision switch
        {
            "APPROVE" => await _inventoryAdminService.ApproveAuditAsync(id, approverId),
            "REJECT" => await _inventoryAdminService.RejectAuditAsync(id, approverId),
            _ => false,
        };

        if (!ok)
        {
            if (decision is not "APPROVE" and not "REJECT")
            {
                return this.BadRequestProblem("Decision must be APPROVE or REJECT.");
            }

            return NotFound();
        }

        return Ok(new { message = decision == "APPROVE" ? "Audit approved." : "Audit rejected." });
    }

    [HttpGet("analytics-reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
        => Ok(await _inventoryAdminService.GetReportsAsync(page, pageSize));

    [HttpGet("audit-entries")]
    public async Task<IActionResult> GetInventoryAuditLog([FromQuery] int take = 300)
        => Ok(await _inventoryAdminService.GetInventoryAuditLogAsync(take));

    [HttpGet("configuration")]
    public async Task<IActionResult> GetSettings() => Ok(await ReadSettingsAsync());

    [HttpPut("configuration")]
    public async Task<IActionResult> SaveSettings([FromBody] InventoryAdminSettings settings)
    {
        settings ??= InventoryAdminSettings.CreateDefault();
        await WriteSettingsAsync(settings);
        return Ok(settings);
    }

    private long? ResolveCurrentUserId()
    {
        return this.GetCurrentUserId();
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Failed to read inventory admin settings from {SettingsPath}; falling back to defaults.", SettingsPath);
            return InventoryAdminSettings.CreateDefault();
        }
    }

    private async Task WriteSettingsAsync(InventoryAdminSettings settings)
    {
        var sanitized = settings with { UpdatedAt = VietnamTime.Now };
        await using var stream = System.IO.File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, sanitized, _jsonOptions);
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
            UpdatedAt = VietnamTime.Now
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

    public sealed record ResolveStockAlertRequestDto
    {
        public long PurchaseOrderId { get; init; }
    }
}
