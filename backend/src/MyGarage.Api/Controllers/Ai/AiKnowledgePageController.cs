using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Ai;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
[Route("api/ai-knowledge-bases/page")]
[Route("api/v1/ai-knowledge-bases/page")]
public sealed class AiKnowledgePageController(AiKnowledgePageQuery query) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AiKnowledgePage>> Get([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Ok(await query.ExecuteAsync(page, pageSize, cancellationToken));
}
