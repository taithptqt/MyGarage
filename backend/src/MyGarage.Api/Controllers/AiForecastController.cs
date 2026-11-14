using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Forecasts;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/ai-forecasts")]
    [Route("api/v1/ai-forecasts")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class AiForecastController : ControllerBase
    {
        private readonly IAiForecastService _aiForecastService;
        private readonly IMapper _mapper;
        private readonly ForecastListQuery _list;

        public AiForecastController(IAiForecastService aiForecastService, IMapper mapper, ForecastListQuery list)
        {
            _aiForecastService = aiForecastService;
            _mapper = mapper;
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
            var item = await _aiForecastService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiForecastResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiForecastResponseDto>> Create([FromBody] AiForecastCreateDto itemDto)
        {
            var entity = _mapper.Map<AiForecast>(itemDto);
            var createdItem = await _aiForecastService.AddAsync(entity);
            var responseDto = _mapper.Map<AiForecastResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiForecastResponseDto>> Update(long id, [FromBody] AiForecastUpdateDto itemDto)
        {
            var existingItem = await _aiForecastService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiForecastService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiForecastResponseDto>(updatedItem);
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






