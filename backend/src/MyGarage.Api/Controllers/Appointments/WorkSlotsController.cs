using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/work-slots")]
    [Route("api/v1/work-slots")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class WorkSlotsController : ControllerBase
    {
        private readonly IWorkSlotService _workSlotService;
        private readonly IAppointmentService _appointmentService;
        private readonly IMapper _mapper;

        public WorkSlotsController(IWorkSlotService workSlotService, IAppointmentService appointmentService, IMapper mapper)
        {
            _workSlotService = workSlotService;
            _appointmentService = appointmentService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<WorkSlotResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _workSlotService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _mapper.Map<List<WorkSlotResponseDto>>(paged);
            return Ok(PagedEnvelope<WorkSlotResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
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

        [HttpPut("{id:long}")]
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

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _workSlotService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        [Authorize]
        [HttpPost("{id:long}/dynamic-extensions")]
        public async Task<ActionResult<WorkSlotDynamicExtensionResponseDto>> ProcessDynamicExtension(
            long id,
            [FromBody] WorkSlotDynamicExtensionRequestDto dto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                var result = await _appointmentService.ProcessDynamicExtensionAsync(id, dto, actorUserId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
