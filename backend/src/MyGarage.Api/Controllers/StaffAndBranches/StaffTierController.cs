using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/staff-tiers")]
    [Route("api/v1/staff-tiers")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class StaffTierController : ControllerBase
    {
        private readonly IStaffTierService _staffTierService;
        private readonly IMapper _mapper;

        public StaffTierController(IStaffTierService staffTierService, IMapper mapper)
        {
            _staffTierService = staffTierService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffTierResponseDto>>> GetAll()
        {
            var items = await _staffTierService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<StaffTierResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StaffTierResponseDto>> GetById(long id)
        {
            var item = await _staffTierService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<StaffTierResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffTierResponseDto>> Create([FromBody] StaffTierCreateDto itemDto)
        {
            var entity = _mapper.Map<StaffTier>(itemDto);
            var createdItem = await _staffTierService.AddAsync(entity);
            var responseDto = _mapper.Map<StaffTierResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StaffTierResponseDto>> Update(long id, [FromBody] StaffTierUpdateDto itemDto)
        {
            var existingItem = await _staffTierService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _staffTierService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<StaffTierResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _staffTierService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






