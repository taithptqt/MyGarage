using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Authorize(Roles = "Staff")]
[Route("api/maintenance-reminders")]
[Route("api/v1/maintenance-reminders")]
[ApiController]
public sealed class MaintenanceRemindersController : ControllerBase
{
    private readonly IMaintenanceReminderService _maintenanceReminderService;

    public MaintenanceRemindersController(IMaintenanceReminderService maintenanceReminderService)
    {
        _maintenanceReminderService = maintenanceReminderService;
    }

    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule(
        [FromBody] MaintenanceReminderScheduleRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Payload is required." });
        }

        try
        {
            var result = await _maintenanceReminderService.ScheduleAsync(
                request,
                this.GetCurrentUserId(),
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("schedule-by-service-order/{serviceOrderId:long}")]
    public async Task<IActionResult> ScheduleByServiceOrder(
        long serviceOrderId,
        [FromQuery] int? remindAfterDays = null,
        [FromQuery] int? remindAfterMileage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _maintenanceReminderService.ScheduleFromServiceOrderAsync(
                serviceOrderId,
                remindAfterDays,
                remindAfterMileage,
                this.GetCurrentUserId(),
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("due-today")]
    public async Task<IActionResult> GetDueToday(CancellationToken cancellationToken)
    {
        var today = VietnamTime.Now.Date;
        var result = await _maintenanceReminderService.GetDueAsync(
            today,
            today.AddDays(1),
            cancellationToken);
        return Ok(new
        {
            date = today,
            count = result.Count,
            items = result
        });
    }

    [HttpPost("process-due")]
    public async Task<IActionResult> ProcessDue(
        [FromQuery] int batchSize = 200,
        CancellationToken cancellationToken = default)
    {
        var result = await _maintenanceReminderService.ProcessDueAsync(batchSize, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> PatchStatus(
        long id,
        [FromBody] MaintenanceReminderStatusPatchRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required." });
        }

        try
        {
            var updated = await _maintenanceReminderService.PatchStatusAsync(
                id,
                request.Status,
                request.CustomerResponse,
                request.ResultAppointmentId,
                cancellationToken);
            if (updated == null)
            {
                return NotFound(new { message = "Reminder not found." });
            }

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
