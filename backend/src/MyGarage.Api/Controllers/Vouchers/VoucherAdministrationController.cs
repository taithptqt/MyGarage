using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Vouchers;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/vouchers/admin-list")]
public sealed class VoucherAdministrationController : ControllerBase
{
    private readonly IVoucherAdministrationService _service;

    public VoucherAdministrationController(IVoucherAdministrationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken,
        [FromQuery] bool includeDeleted = true)
    {
        var result = await _service.GetPageAsync(search, status, page, pageSize, includeDeleted, cancellationToken);
        return Ok(new { items = result.Items, total = result.Total, statuses = result.Statuses });
    }

    // Totals and sorting match the legacy voucher report's unfiltered voucher collection.
    [HttpGet("~/api/v1/vouchers/report-page")]
    public async Task<IActionResult> GetReportPage([FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var report = await _service.GetReportPageAsync(page, pageSize, cancellationToken);
        return Ok(new
        {
            items = report.Items,
            total = report.Total,
            issued = report.Issued,
            redeemed = report.Redeemed,
            budgetLimit = report.BudgetLimit,
            budgetUsed = report.BudgetUsed
        });
    }

    // Global dashboard combines voucher, redemption and approval aggregates and therefore requires admin.
    [Authorize(Policy = "AdminStaffOnly")]
    [HttpGet("~/api/v1/vouchers/dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _service.GetDashboardAsync(cancellationToken);
        return Ok(new
        {
            activeCount = dashboard.ActiveCount,
            issued = dashboard.Issued,
            redeemed = dashboard.Redeemed,
            budgetLimit = dashboard.BudgetLimit,
            budgetUsed = dashboard.BudgetUsed,
            totalDiscount = dashboard.TotalDiscount,
            pendingApprovals = dashboard.PendingApprovals,
            topVouchers = dashboard.TopVouchers,
            nearQuotaVouchers = dashboard.NearQuotaVouchers.Select(x => new { x.Id, x.Code, x.VoucherName, ratio = x.Ratio }),
            approvalItems = dashboard.ApprovalItems.Select(x => new { x.Id, x.RequestType, x.RiskLevel })
        });
    }
}
