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
    public class AiCustomerInsightController : ControllerBase
    {
        private readonly IAiCustomerInsightService _aiCustomerInsightService;
        private readonly IMapper _mapper;

        public AiCustomerInsightController(IAiCustomerInsightService aiCustomerInsightService, IMapper mapper)
        {
            _aiCustomerInsightService = aiCustomerInsightService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiCustomerInsightResponseDto>>> GetAll()
        {
            var items = await _aiCustomerInsightService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiCustomerInsightResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiCustomerInsightResponseDto>> GetById(long id)
        {
            var item = await _aiCustomerInsightService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiCustomerInsightResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiCustomerInsightResponseDto>> Create([FromBody] AiCustomerInsightCreateDto itemDto)
        {
            var entity = _mapper.Map<AiCustomerInsight>(itemDto);
            var createdItem = await _aiCustomerInsightService.AddAsync(entity);
            var responseDto = _mapper.Map<AiCustomerInsightResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiCustomerInsightResponseDto>> Update(long id, [FromBody] AiCustomerInsightUpdateDto itemDto)
        {
            var existingItem = await _aiCustomerInsightService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiCustomerInsightService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiCustomerInsightResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiCustomerInsightService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





