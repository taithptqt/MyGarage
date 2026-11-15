using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/work-slots")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class WorkSlotsController : ControllerBase
    {
        private readonly IWorkSlotService _workSlotService;
        private readonly IAppointmentSlotHoldService _appointmentSlotHoldService;

        public WorkSlotsController(IWorkSlotService workSlotService, IAppointmentSlotHoldService appointmentSlotHoldService)
        {
            _workSlotService = workSlotService;
            _appointmentSlotHoldService = appointmentSlotHoldService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<WorkSlotResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _workSlotService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _workSlotService.MapToDtos(paged);
            return Ok(PagedEnvelope<WorkSlotResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<WorkSlotResponseDto>> GetById(long id)
        {
            var dto = await _workSlotService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<WorkSlotResponseDto>> Create([FromBody] WorkSlotCreateDto itemDto)
        {
            var responseDto = await _workSlotService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<WorkSlotResponseDto>> Update(long id, [FromBody] WorkSlotUpdateDto itemDto)
        {
            var responseDto = await _workSlotService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

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
                var result = await _appointmentSlotHoldService.ProcessDynamicExtensionAsync(id, dto, actorUserId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return this.NotFoundProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }
    }
}
