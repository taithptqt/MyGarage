using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Ai;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/ai-smart-dispatchings")]
    [Route("api/v1/ai-smart-dispatchings")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class AiSmartDispatchingController : ControllerBase
    {
        private readonly IAiSmartDispatchingService _aiSmartDispatchingService;
        private readonly IMapper _mapper;
        private readonly DispatchListQuery _list;

        public AiSmartDispatchingController(IAiSmartDispatchingService aiSmartDispatchingService, IMapper mapper, DispatchListQuery list)
        {
            _aiSmartDispatchingService = aiSmartDispatchingService;
            _mapper = mapper;
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
            var item = await _aiSmartDispatchingService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiSmartDispatchingResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiSmartDispatchingResponseDto>> Create([FromBody] AiSmartDispatchingCreateDto itemDto)
        {
            var entity = _mapper.Map<AiSmartDispatching>(itemDto);
            var createdItem = await _aiSmartDispatchingService.AddAsync(entity);
            var responseDto = _mapper.Map<AiSmartDispatchingResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiSmartDispatchingResponseDto>> Update(long id, [FromBody] AiSmartDispatchingUpdateDto itemDto)
        {
            var existingItem = await _aiSmartDispatchingService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiSmartDispatchingService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiSmartDispatchingResponseDto>(updatedItem);
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






