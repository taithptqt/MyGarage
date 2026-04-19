using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/ai-smart-dispatchings")]
    [ApiController]
    public class AiSmartDispatchingController : ControllerBase
    {
        private readonly IAiSmartDispatchingService _aiSmartDispatchingService;
        private readonly IMapper _mapper;

        public AiSmartDispatchingController(IAiSmartDispatchingService aiSmartDispatchingService, IMapper mapper)
        {
            _aiSmartDispatchingService = aiSmartDispatchingService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiSmartDispatchingResponseDto>>> GetAll()
        {
            var items = await _aiSmartDispatchingService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiSmartDispatchingResponseDto>>(items);
            return Ok(dtos);
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






