using System;
using System.Collections.Generic;
using System.Security.Claims;
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
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IMapper _mapper;

        public AppointmentController(IAppointmentService appointmentService, IMapper mapper)
        {
            _appointmentService = appointmentService;
            _mapper = mapper;
        }

        // ── Generic CRUD (kept for backward compatibility) ──────────────────

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppointmentResponseDto>>> GetAll()
        {
            var items = await _appointmentService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AppointmentResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AppointmentResponseDto>> GetById(long id)
        {
            var item = await _appointmentService.GetByIdAsync(id);
            if (item == null) return NotFound();
            var dto = _mapper.Map<AppointmentResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AppointmentResponseDto>> Create([FromBody] AppointmentCreateDto itemDto)
        {
            var entity = _mapper.Map<Appointment>(itemDto);
            var createdItem = await _appointmentService.AddAsync(entity);
            var responseDto = _mapper.Map<AppointmentResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AppointmentResponseDto>> Update(long id, [FromBody] AppointmentUpdateDto itemDto)
        {
            var existingItem = await _appointmentService.GetByIdAsync(id);
            if (existingItem == null) return NotFound();

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _appointmentService.UpdateAsync(id, existingItem);
            if (updatedItem == null) return NotFound();

            var responseDto = _mapper.Map<AppointmentResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _appointmentService.DeleteAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        // ── Business endpoints ──────────────────────────────────────────────

        [HttpGet("available-slots")]
        public async Task<ActionResult<IEnumerable<AvailableSlotResponseDto>>> GetAvailableSlots(
            [FromQuery] long branchId,
            [FromQuery] DateOnly date,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] string? bookingFlow = null)
        {
            var slots = await _appointmentService.GetAvailableSlotsAsync(branchId, date, requestedServiceIds, bookingFlow);
            return Ok(slots);
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<AppointmentDetailResponseDto>>> GetMyAppointments()
        {
            var customerId = GetUserIdFromToken();
            if (customerId == null) return Unauthorized();

            var appointments = await _appointmentService.GetCustomerAppointmentsAsync(customerId.Value);
            return Ok(appointments);
        }

        [Authorize]
        [HttpGet("branch/{branchId}")]
        public async Task<ActionResult<IEnumerable<AppointmentDetailResponseDto>>> GetBranchAppointments(
            long branchId, [FromQuery] string? status = null)
        {
            var appointments = await _appointmentService.GetBranchAppointmentsAsync(branchId, status);
            return Ok(appointments);
        }

        [Authorize]
        [HttpGet("detail/{id}")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> GetDetailed(long id)
        {
            var dto = await _appointmentService.GetDetailedAsync(id);
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        [Authorize]
        [HttpPost("book")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> Book([FromBody] BookAppointmentRequestDto dto)
        {
            var authUserId = GetUserIdFromToken();
            if (authUserId == null) return Unauthorized();

            dto.CreatedBy = authUserId;

            try
            {
                var appointment = await _appointmentService.BookAsync(authUserId.Value, dto);
                var detail = await _appointmentService.GetDetailedAsync(appointment.Id);
                return CreatedAtAction(nameof(GetDetailed), new { id = appointment.Id }, detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("{id}/confirm")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> Confirm(long id)
        {
            try
            {
                var actorUserId = GetUserIdFromToken();
                var appointment = await _appointmentService.ConfirmAsync(id, actorUserId);
                if (appointment == null) return NotFound();

                var detail = await _appointmentService.GetDetailedAsync(id);
                return Ok(detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("{id}/cancel")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> Cancel(long id, [FromBody] CancelAppointmentRequestDto? dto)
        {
            try
            {
                var actorUserId = GetUserIdFromToken();
                var appointment = await _appointmentService.CancelAsync(id, dto?.Reason, actorUserId);
                if (appointment == null) return NotFound();

                var detail = await _appointmentService.GetDetailedAsync(id);
                return Ok(detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("{id}/check-in")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> CheckIn(long id)
        {
            try
            {
                var actorUserId = GetUserIdFromToken();
                var appointment = await _appointmentService.CheckInAsync(id, actorUserId);
                if (appointment == null) return NotFound();

                var detail = await _appointmentService.GetDetailedAsync(id);
                return Ok(detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("book-batch")]
        public async Task<ActionResult<BookingGroupResponseDto>> BookBatch([FromBody] BookAppointmentBatchRequestDto dto)
        {
            var authUserId = GetUserIdFromToken();
            if (authUserId == null) return Unauthorized();

            try
            {
                var result = await _appointmentService.BookBatchAsync(authUserId.Value, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("{id}/arrive")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> Arrive(long id, [FromBody] ArriveAppointmentRequestDto? dto)
        {
            try
            {
                var actorUserId = GetUserIdFromToken();
                var appointment = await _appointmentService.ArriveAsync(id, dto?.ArrivedAt, actorUserId);
                if (appointment == null) return NotFound();

                var detail = await _appointmentService.GetDetailedAsync(id);
                return Ok(detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("branch/{branchId}/auto-no-show")]
        public async Task<ActionResult<object>> AutoMarkNoShows(long branchId, [FromQuery] DateTime? now = null)
        {
            var affected = await _appointmentService.AutoMarkNoShowsAsync(branchId, now);
            return Ok(new { updated = affected });
        }

        [Authorize]
        [HttpGet("branch/{branchId}/booking-health")]
        public async Task<ActionResult<BranchBookingHealthResponseDto>> GetBookingHealth(long branchId, [FromQuery] DateTime? at = null)
        {
            var health = await _appointmentService.GetBranchBookingHealthAsync(branchId, at);
            return Ok(health);
        }

        [Authorize]
        [HttpGet("branch/{branchId}/dispatch-dashboard")]
        public async Task<ActionResult<DispatchDashboardResponseDto>> GetDispatchDashboard(long branchId, [FromQuery] DateTime? at = null)
        {
            var dashboard = await _appointmentService.GetDispatchDashboardAsync(branchId, at);
            return Ok(dashboard);
        }

        [Authorize]
        [HttpPost("branch/{branchId}/dispatch-cycle")]
        public async Task<ActionResult<DispatchCycleResponseDto>> RunDispatchCycle(long branchId, [FromQuery] DateTime? at = null)
        {
            var result = await _appointmentService.RunDispatchCycleAsync(branchId, at);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("branch/{branchId}/overnight-lock")]
        public async Task<ActionResult<OvernightLockResponseDto>> LockOvernightSlots(long branchId, [FromBody] OvernightLockRequestDto? dto)
        {
            var result = await _appointmentService.LockOvernightSlotsAsync(branchId, dto?.BusinessDate);
            return Ok(result);
        }

        private long? GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && long.TryParse(claim.Value, out var id))
                return id;
            return null;
        }
    }

    public class CancelAppointmentRequestDto
    {
        public string? Reason { get; set; }
    }
}
