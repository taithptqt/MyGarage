using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Forecasts;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/ai-forecasts")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class AiForecastController : ControllerBase
    {
        private readonly IAiForecastService _aiForecastService;
        private readonly ForecastListQuery _list;

        public AiForecastController(IAiForecastService aiForecastService, ForecastListQuery list)
        {
            _aiForecastService = aiForecastService;
            _list = list;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize);
            var result = await _list.ExecuteAsync(current, size, cancellationToken);
            return Ok(PagedEnvelope<ForecastRow>.Create(result.Items, result.Total, current, size));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiForecastResponseDto>> GetById(long id)
        {
            var dto = await _aiForecastService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiForecastResponseDto>> Create([FromBody] AiForecastCreateDto itemDto)
        {
            var responseDto = await _aiForecastService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiForecastResponseDto>> Update(long id, [FromBody] AiForecastUpdateDto itemDto)
        {
            var responseDto = await _aiForecastService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiForecastService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






