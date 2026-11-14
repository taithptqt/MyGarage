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
    [Authorize(Roles = "Staff")]
    [Route("api/revenue-snapshots")]
    [Route("api/v1/revenue-snapshots")]
    [ApiController]
    public class RevenueSnapshotController : ControllerBase
    {
        private readonly IRevenueSnapshotService _revenueSnapshotService;
        private readonly IMapper _mapper;

        public RevenueSnapshotController(IRevenueSnapshotService revenueSnapshotService, IMapper mapper)
        {
            _revenueSnapshotService = revenueSnapshotService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RevenueSnapshotResponseDto>>> GetAll()
        {
            var items = await _revenueSnapshotService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RevenueSnapshotResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> GetById(long id)
        {
            var item = await _revenueSnapshotService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RevenueSnapshotResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> Create([FromBody] RevenueSnapshotCreateDto itemDto)
        {
            var entity = _mapper.Map<RevenueSnapshot>(itemDto);
            var createdItem = await _revenueSnapshotService.AddAsync(entity);
            var responseDto = _mapper.Map<RevenueSnapshotResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> Update(long id, [FromBody] RevenueSnapshotUpdateDto itemDto)
        {
            var existingItem = await _revenueSnapshotService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _revenueSnapshotService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RevenueSnapshotResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _revenueSnapshotService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






