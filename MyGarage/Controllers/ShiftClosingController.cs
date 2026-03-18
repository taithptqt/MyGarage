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
    public class ShiftClosingController : ControllerBase
    {
        private readonly IShiftClosingService _shiftClosingService;
        private readonly IMapper _mapper;

        public ShiftClosingController(IShiftClosingService shiftClosingService, IMapper mapper)
        {
            _shiftClosingService = shiftClosingService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShiftClosingResponseDto>>> GetAll()
        {
            var items = await _shiftClosingService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ShiftClosingResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftClosingResponseDto>> GetById(long id)
        {
            var item = await _shiftClosingService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ShiftClosingResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ShiftClosingResponseDto>> Create([FromBody] ShiftClosingCreateDto itemDto)
        {
            var entity = _mapper.Map<ShiftClosing>(itemDto);
            var createdItem = await _shiftClosingService.AddAsync(entity);
            var responseDto = _mapper.Map<ShiftClosingResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ShiftClosingResponseDto>> Update(long id, [FromBody] ShiftClosingUpdateDto itemDto)
        {
            var existingItem = await _shiftClosingService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _shiftClosingService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ShiftClosingResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _shiftClosingService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





