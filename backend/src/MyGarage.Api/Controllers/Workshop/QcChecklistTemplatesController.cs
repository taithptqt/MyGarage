using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api/v1/qc-checklist-templates")]
[ApiController]
[Authorize]
public sealed class QcChecklistTemplatesController : ControllerBase
{
    private readonly IQcChecklistTemplateCommandService _commandService;

    public QcChecklistTemplatesController(IQcChecklistTemplateCommandService commandService)
    {
        _commandService = commandService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] string? serviceType = null,
        [FromQuery] long? serviceOrderId = null,
        [FromQuery] bool includeInactive = false)
    {
        var result = await _commandService.GetTemplatesAsync(
            serviceType,
            serviceOrderId,
            includeInactive,
            HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("/api/v1/qc-templates")]
    public async Task<IActionResult> GetTemplatesList(
        [FromQuery] string? serviceType = null,
        [FromQuery] bool includeInactive = true)
    {
        var rows = await _commandService.GetTemplatesListAsync(
            serviceType,
            includeInactive,
            HttpContext.RequestAborted);
        return Ok(rows);
    }

    [HttpGet("{id:long}")]
    [HttpGet("/api/v1/qc-templates/{id:long}")]
    public async Task<IActionResult> GetTemplateById(long id)
    {
        var result = await _commandService.GetByIdAsync(id, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpPost]
    [HttpPost("/api/v1/qc-templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] QcTemplateUpsertDto? payload)
    {
        if (!IsQcTemplateAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _commandService.CreateAsync(payload, HttpContext.RequestAborted);
        return result.Status switch
        {
            QcChecklistTemplateCommandStatus.Created => CreatedAtAction(nameof(GetTemplateById), new { id = result.Data!.Id }, result.Data),
            _ => ToActionResult(result)
        };
    }

    [HttpPut("{id:long}")]
    [HttpPut("/api/v1/qc-templates/{id:long}")]
    public async Task<IActionResult> UpdateTemplate(long id, [FromBody] QcTemplateUpsertDto? payload)
    {
        if (!IsQcTemplateAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _commandService.UpdateAsync(id, payload, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpDelete("{id:long}")]
    [HttpDelete("/api/v1/qc-templates/{id:long}")]
    public async Task<IActionResult> DeleteTemplate(long id)
    {
        if (!IsQcTemplateAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _commandService.DeleteAsync(id, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    private bool IsQcTemplateAuthorizedStaff() =>
        this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC", "QC");

    private IActionResult ToActionResult<T>(QcChecklistTemplateCommandResult<T> result) => result.Status switch
    {
        QcChecklistTemplateCommandStatus.NoContent => NoContent(),
        QcChecklistTemplateCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? string.Empty),
        QcChecklistTemplateCommandStatus.NotFound => this.NotFoundProblem(result.ErrorMessage ?? string.Empty),
        _ => Ok(result.Data)
    };
}

