using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/v1/appointments/reception-list")]
[Authorize(Roles = "Staff")]
public class ReceptionAppointmentListController(IReceptionAppointmentListQueryService queryService, IAppointmentDetailAssembler assembler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? branchId, [FromQuery] string? search,
        [FromQuery] string? status, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? sort, [FromQuery] string? mode, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied)) return (IActionResult)denied!;
        var result = await queryService.GetAsync(scopedBranchId, search, status, dateFrom, dateTo, sort, mode, page, pageSize, HttpContext.RequestAborted);
        var items = await assembler.AssembleManyAsync(result.Entities);
        return Ok(new { items, singleCount = result.SingleCount, groupCount = result.GroupCount, statuses = result.Statuses });
    }
}
