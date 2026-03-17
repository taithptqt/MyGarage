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
    public class ServiceOrderSnapshotController : ControllerBase
    {
        private readonly IServiceOrderSnapshotService _serviceOrderSnapshotService;
        private readonly IMapper _mapper;

        public ServiceOrderSnapshotController(IServiceOrderSnapshotService serviceOrderSnapshotService, IMapper mapper)
        {
            _serviceOrderSnapshotService = serviceOrderSnapshotService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderSnapshotResponseDto>>> GetAll()
        {
            var items = await _serviceOrderSnapshotService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceOrderSnapshotResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderSnapshotResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderSnapshotService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceOrderSnapshotResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderSnapshotResponseDto>> Create([FromBody] ServiceOrderSnapshotCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceOrderSnapshot>(itemDto);
            var createdItem = await _serviceOrderSnapshotService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceOrderSnapshotResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderSnapshotResponseDto>> Update(long id, [FromBody] ServiceOrderSnapshotUpdateDto itemDto)
        {
            var existingItem = await _serviceOrderSnapshotService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceOrderSnapshotService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ServiceOrderSnapshotResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceOrderSnapshotService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





