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
    public class BranchOperatingHourController : ControllerBase
    {
        private readonly IBranchOperatingHourService _branchOperatingHourService;
        private readonly IMapper _mapper;

        public BranchOperatingHourController(IBranchOperatingHourService branchOperatingHourService, IMapper mapper)
        {
            _branchOperatingHourService = branchOperatingHourService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchOperatingHourResponseDto>>> GetAll()
        {
            var items = await _branchOperatingHourService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<BranchOperatingHourResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BranchOperatingHourResponseDto>> GetById(long id)
        {
            var item = await _branchOperatingHourService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<BranchOperatingHourResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BranchOperatingHourResponseDto>> Create([FromBody] BranchOperatingHourCreateDto itemDto)
        {
            var entity = _mapper.Map<BranchOperatingHour>(itemDto);
            var createdItem = await _branchOperatingHourService.AddAsync(entity);
            var responseDto = _mapper.Map<BranchOperatingHourResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BranchOperatingHourResponseDto>> Update(long id, [FromBody] BranchOperatingHourUpdateDto itemDto)
        {
            var existingItem = await _branchOperatingHourService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _branchOperatingHourService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<BranchOperatingHourResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _branchOperatingHourService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





