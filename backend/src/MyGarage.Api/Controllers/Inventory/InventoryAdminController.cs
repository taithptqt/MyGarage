using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api/v1/inventory-management")]
[ApiController]
[Authorize]
public class InventoryAdminController : ControllerBase
{
    private readonly IInventoryAdminService _inventoryAdminService;
    private readonly IInventoryAuthorizationService _inventoryAuthService;
    private readonly IInventoryAdminSettingsService _settingsService;

    public InventoryAdminController(
        IInventoryAdminService inventoryAdminService,
        IInventoryAuthorizationService inventoryAuthService,
        IInventoryAdminSettingsService settingsService)
    {
        _inventoryAdminService = inventoryAdminService;
        _inventoryAuthService = inventoryAuthService;
        _settingsService = settingsService;
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
    public async Task<IActionResult> GetSettings()
        => Ok(await _settingsService.GetSettingsAsync(HttpContext.RequestAborted));

    [HttpPut("configuration")]
    public async Task<IActionResult> SaveSettings([FromBody] InventoryAdminSettings settings)
        => Ok(await _settingsService.SaveSettingsAsync(settings, HttpContext.RequestAborted));

    private long? ResolveCurrentUserId()
    {
        return this.GetCurrentUserId();
    }
}
