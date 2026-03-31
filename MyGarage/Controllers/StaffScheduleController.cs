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
    [Route("api/staff-schedules")]
[Route("api/[controller]")]
    [ApiController]
    public class StaffScheduleController : ControllerBase
    {
        private readonly IStaffScheduleService _staffScheduleService;
        private readonly IMapper _mapper;

        public StaffScheduleController(IStaffScheduleService staffScheduleService, IMapper mapper)
        {
            _staffScheduleService = staffScheduleService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffScheduleResponseDto>>> GetAll()
        {
            var items = await _staffScheduleService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<StaffScheduleResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StaffScheduleResponseDto>> GetById(long id)
        {
            var item = await _staffScheduleService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<StaffScheduleResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffScheduleResponseDto>> Create([FromBody] StaffScheduleCreateDto itemDto)
        {
            var entity = _mapper.Map<StaffSchedule>(itemDto);
            var createdItem = await _staffScheduleService.AddAsync(entity);
            var responseDto = _mapper.Map<StaffScheduleResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StaffScheduleResponseDto>> Update(long id, [FromBody] StaffScheduleUpdateDto itemDto)
        {
            var existingItem = await _staffScheduleService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _staffScheduleService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<StaffScheduleResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _staffScheduleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






