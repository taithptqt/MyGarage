using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Ai;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
[Route("api/ai-model-configs/page")]
[Route("api/v1/ai-model-configs/page")]
public sealed class AiModelConfigPageController(AiModelConfigPageQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AiModelConfigPage>> Get([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Ok(await query.ExecuteAsync(page, pageSize, cancellationToken));
}
