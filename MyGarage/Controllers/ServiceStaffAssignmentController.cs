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
    [Route("api/service-staff-assignments")]
    [ApiController]
    public class ServiceStaffAssignmentController : ControllerBase
    {
        private readonly IServiceStaffAssignmentService _serviceStaffAssignmentService;
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentController(IServiceStaffAssignmentService serviceStaffAssignmentService, IMapper mapper)
        {
            _serviceStaffAssignmentService = serviceStaffAssignmentService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> GetAll()
        {
            var items = await _serviceStaffAssignmentService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceStaffAssignmentResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> GetById(long id)
        {
            var item = await _serviceStaffAssignmentService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceStaffAssignmentResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Create([FromBody] ServiceStaffAssignmentCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceStaffAssignment>(itemDto);
            var createdItem = await _serviceStaffAssignmentService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Update(long id, [FromBody] ServiceStaffAssignmentUpdateDto itemDto)
        {
            var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceStaffAssignmentService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceStaffAssignmentService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






