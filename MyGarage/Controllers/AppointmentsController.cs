using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/appointments")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        public sealed record AppointmentStatusPatchRequestDto
        {
            public string? Status { get; init; }
            public string? Reason { get; init; }
            public DateTime? ArrivedAt { get; init; }
        }

        private readonly IAppointmentService _appointmentService;

        public AppointmentsController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        [HttpGet("slots")]
        public async Task<ActionResult<IEnumerable<AvailableSlotResponseDto>>> GetAvailability(
            [FromQuery] long branchId,
            [FromQuery] DateOnly date,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] string? bookingFlow = null)
        {
            var slots = await _appointmentService.GetAvailableSlotsAsync(branchId, date, requestedServiceIds, bookingFlow);
            return Ok(slots);
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppointmentDetailResponseDto>>> GetAll([FromQuery] string? status = null)
        {
            var appointments = await _appointmentService.GetAllDetailedAsync(status);
            return Ok(appointments);
        }

        [Authorize]
        [HttpGet("customers/current")]
        public async Task<ActionResult<IEnumerable<AppointmentDetailResponseDto>>> GetMine()
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null) return Unauthorized();

            var appointments = await _appointmentService.GetCustomerAppointmentsAsync(customerId.Value);
            return Ok(appointments);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}")]
        public async Task<ActionResult<IEnumerable<AppointmentDetailResponseDto>>> GetByBranch(long branchId, [FromQuery] string? status = null)
        {
            var appointments = await _appointmentService.GetBranchAppointmentsAsync(branchId, status);
            return Ok(appointments);
        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> GetById(long id)
        {
            var dto = await _appointmentService.GetDetailedAsync(id);
            if (dto == null) return NotFound();
            return Ok(dto);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<AppointmentDetailResponseDto>> Create([FromBody] BookAppointmentRequestDto dto)
        {
            var authUserId = this.GetCurrentUserId();
            if (authUserId == null) return Unauthorized();

            dto.CreatedBy = authUserId;

            try
            {
                var appointment = await _appointmentService.BookAsync(authUserId.Value, dto);
                var detail = await _appointmentService.GetDetailedAsync(appointment.Id);
                return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("public-bookings")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> CreateGuest([FromBody] BookAppointmentRequestDto dto)
        {
            if (!dto.CustomerId.HasValue)
            {
                return BadRequest(new { message = "Vui long cung cap CustomerId cho luong guest." });
            }

            try
            {
                dto.CreatedBy = null;
                var appointment = await _appointmentService.BookAsync(dto.CustomerId.Value, dto);
                var detail = await _appointmentService.GetDetailedAsync(appointment.Id);
                return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPatch("{id:long}")]
        public async Task<ActionResult<AppointmentDetailResponseDto>> UpdateStatus(long id, [FromBody] AppointmentStatusPatchRequestDto dto)
        {
            var requested = (dto?.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(requested))
            {
                return BadRequest(new { message = "Status is required." });
            }

            try
            {
                var actorUserId = this.GetCurrentUserId();
                global::MyGarage.Models.Appointment? appointment = requested switch
                {
                    "CONFIRMED" => await _appointmentService.ConfirmAsync(id, actorUserId),
                    "CANCELED" => await _appointmentService.CancelAsync(id, dto?.Reason, actorUserId),
                    "CHECKED_IN" => await _appointmentService.CheckInAsync(id, actorUserId),
                    "ARRIVED" => await _appointmentService.ArriveAsync(id, dto?.ArrivedAt, actorUserId),
                    _ => null
                };

                if (appointment == null)
                {
                    if (requested is "CONFIRMED" or "CANCELED" or "CHECKED_IN" or "ARRIVED")
                    {
                        return NotFound();
                    }

                    return BadRequest(new { message = "Unsupported status transition." });
                }

                var detail = await _appointmentService.GetDetailedAsync(id);
                return Ok(detail);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/no-show-cycles")]
        public async Task<ActionResult<object>> RunNoShow(long branchId, [FromQuery] DateTime? now = null)
        {
            var affected = await _appointmentService.AutoMarkNoShowsAsync(branchId, now);
            return Ok(new { updated = affected });
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/health-metrics")]
        public async Task<ActionResult<BranchBookingHealthResponseDto>> GetBookingHealth(long branchId, [FromQuery] DateTime? at = null)
        {
            var health = await _appointmentService.GetBranchBookingHealthAsync(branchId, at);
            return Ok(health);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/allocation-metrics")]
        public async Task<ActionResult<DispatchDashboardResponseDto>> GetDispatchDashboard(long branchId, [FromQuery] DateTime? at = null)
        {
            var dashboard = await _appointmentService.GetDispatchDashboardAsync(branchId, at);
            return Ok(dashboard);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/allocation-cycles")]
        public async Task<ActionResult<DispatchCycleResponseDto>> CreateDispatchRun(long branchId, [FromQuery] DateTime? at = null)
        {
            var result = await _appointmentService.RunDispatchCycleAsync(branchId, at);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/overnight-slot-constraints")]
        public async Task<ActionResult<OvernightLockResponseDto>> CreateOvernightLock(long branchId, [FromBody] OvernightLockRequestDto? dto)
        {
            var result = await _appointmentService.LockOvernightSlotsAsync(branchId, dto?.BusinessDate);
            return Ok(result);
        }
    }
}
