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
    public class WorkSlotController : ControllerBase
    {
        private readonly IWorkSlotService _workSlotService;
        private readonly IMapper _mapper;

        public WorkSlotController(IWorkSlotService workSlotService, IMapper mapper)
        {
            _workSlotService = workSlotService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkSlotResponseDto>>> GetAll()
        {
            var items = await _workSlotService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<WorkSlotResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WorkSlotResponseDto>> GetById(long id)
        {
            var item = await _workSlotService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<WorkSlotResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<WorkSlotResponseDto>> Create([FromBody] WorkSlotCreateDto itemDto)
        {
            var entity = _mapper.Map<WorkSlot>(itemDto);
            var createdItem = await _workSlotService.AddAsync(entity);
            var responseDto = _mapper.Map<WorkSlotResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WorkSlotResponseDto>> Update(long id, [FromBody] WorkSlotUpdateDto itemDto)
        {
            var existingItem = await _workSlotService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _workSlotService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<WorkSlotResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _workSlotService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





