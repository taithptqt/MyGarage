using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

/// <summary>
/// Customer-scoped portal API. Uses JWT NameIdentifier as Customer.Id; does not accept customerId from clients.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/customers/me")]
public sealed class CustomersMeController : ControllerBase
{
    private readonly ICustomerPortalService _portal;

    public CustomersMeController(ICustomerPortalService portal)
    {
        _portal = portal;
    }

    private bool TryResolveCustomerId(out long customerId, out ActionResult? errorResult)
    {
        errorResult = null;
        var id = this.GetCurrentUserId();
        if (id == null)
        {
            errorResult = Unauthorized();
            customerId = 0;
            return false;
        }

        if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
        {
            errorResult = Forbid();
            customerId = 0;
            return false;
        }

        customerId = id.Value;
        return true;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        var data = await _portal.GetDashboardAsync(customerId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult> GetVehicles(CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        var list = await _portal.GetVehiclesAsync(customerId, cancellationToken);
        return Ok(list);
    }

    [HttpPost("vehicles")]
    public async Task<ActionResult> CreateVehicle([FromBody] CustomerPortalVehicleUpsertDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var created = await _portal.CreateVehicleAsync(customerId, dto, cancellationToken);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }
    }

    [HttpPut("vehicles/{vehicleId:long}")]
    public async Task<ActionResult> UpdateVehicle(long vehicleId, [FromBody] CustomerPortalVehicleUpsertDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        try
        {
            var updated = await _portal.UpdateVehicleAsync(customerId, vehicleId, dto, cancellationToken);
            if (updated == null)
                return NotFound();
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }
    }

    [HttpGet("vehicles/{vehicleId:long}/history")]
    public async Task<ActionResult> GetVehicleHistory(long vehicleId, CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        var data = await _portal.GetVehicleHistoryAsync(customerId, vehicleId, cancellationToken);
        if (data == null)
            return NotFound();
        return Ok(data);
    }

    [HttpGet("notifications")]
    public async Task<ActionResult> GetNotifications([FromQuery] bool? unreadOnly, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedSize = Math.Clamp(pageSize.GetValueOrDefault(200), 1, 200);
        var (items, unreadCount, totalCount) = await _portal.GetNotificationsAsync(customerId, unreadOnly, normalizedPage, normalizedSize, cancellationToken);
        return Ok(new { unreadCount, items, totalCount, page = normalizedPage, pageSize = normalizedSize });
    }

    [HttpPost("notifications/{id:long}/reads")]
    public async Task<ActionResult> MarkNotificationRead(long id, CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        var ok = await _portal.MarkNotificationReadAsync(customerId, id, cancellationToken);
        if (!ok)
            return NotFound();
        return NoContent();
    }

    [HttpPost("notifications/bulk-reads")]
    public async Task<ActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;
        var n = await _portal.MarkAllNotificationsReadAsync(customerId, cancellationToken);
        return Ok(new { updatedCount = n });
    }

    [HttpGet("vehicle-lookups")]
    public async Task<ActionResult> GetVehicleLookups(CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out _, out var err))
            return err!;
        var data = await _portal.GetVehicleLookupsAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("service-orders/{serviceOrderId:long}/link-context")]
    public async Task<ActionResult> GetServiceOrderLinkContext(long serviceOrderId, CancellationToken cancellationToken)
    {
        if (!TryResolveCustomerId(out var customerId, out var err))
            return err!;

        var data = await _portal.GetServiceOrderLinkContextAsync(customerId, serviceOrderId, cancellationToken);
        if (data == null)
            return NotFound();

        return Ok(data);
    }
}
