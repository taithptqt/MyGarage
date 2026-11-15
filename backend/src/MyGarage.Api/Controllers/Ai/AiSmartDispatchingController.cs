using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Ai;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/ai-smart-dispatchings")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class AiSmartDispatchingController : ControllerBase
    {
        private readonly IAiSmartDispatchingService _aiSmartDispatchingService;
        private readonly DispatchListQuery _list;

        public AiSmartDispatchingController(IAiSmartDispatchingService aiSmartDispatchingService, DispatchListQuery list)
        {
            _aiSmartDispatchingService = aiSmartDispatchingService;
            _list = list;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize);
            var result = await _list.ExecuteAsync(current, size, cancellationToken);
            return Ok(PagedEnvelope<DispatchRow>.Create(result.Items, result.Total, current, size));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiSmartDispatchingResponseDto>> GetById(long id)
        {
            var dto = await _aiSmartDispatchingService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiSmartDispatchingResponseDto>> Create([FromBody] AiSmartDispatchingCreateDto itemDto)
        {
            var responseDto = await _aiSmartDispatchingService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiSmartDispatchingResponseDto>> Update(long id, [FromBody] AiSmartDispatchingUpdateDto itemDto)
        {
            var responseDto = await _aiSmartDispatchingService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiSmartDispatchingService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpGet("recommend-bay/{serviceOrderId}")]
        public async Task<ActionResult<AiDispatchRecommendationResponseDto>> RecommendBay(long serviceOrderId)
        {
            var recommendation = await _aiSmartDispatchingService.RecommendBayAsync(serviceOrderId);
            if (recommendation == null) return NotFound();
            return Ok(recommendation);
        }
    }
}






