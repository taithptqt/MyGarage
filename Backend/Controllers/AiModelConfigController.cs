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
    [Route("api/[controller]")]
    [ApiController]
    public class AiModelConfigController : ControllerBase
    {
        private readonly IAiModelConfigService _aiModelConfigService;
        private readonly IMapper _mapper;

        public AiModelConfigController(IAiModelConfigService aiModelConfigService, IMapper mapper)
        {
            _aiModelConfigService = aiModelConfigService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiModelConfigResponseDto>>> GetAll()
        {
            var items = await _aiModelConfigService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiModelConfigResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiModelConfigResponseDto>> GetById(long id)
        {
            var item = await _aiModelConfigService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiModelConfigResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiModelConfigResponseDto>> Create([FromBody] AiModelConfigCreateDto itemDto)
        {
            var entity = _mapper.Map<AiModelConfig>(itemDto);
            var createdItem = await _aiModelConfigService.AddAsync(entity);
            var responseDto = _mapper.Map<AiModelConfigResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiModelConfigResponseDto>> Update(long id, [FromBody] AiModelConfigUpdateDto itemDto)
        {
            var existingItem = await _aiModelConfigService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiModelConfigService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiModelConfigResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiModelConfigService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





