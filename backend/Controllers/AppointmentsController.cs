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
            [FromQuery] string? bookingFlow = null,
            [FromQuery] int vehicleCount = 1,
            [FromQuery] bool isParallel = true)
        {
                    var slots = await _appointmentService.GetAvailableSlotsAsync(branchId, date, requestedServiceIds, bookingFlow, vehicleCount, isParallel);
            return Ok(slots);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/nearest-slot")]
        public async Task<ActionResult<NearestSlotSuggestionResponseDto>> GetNearestSlot(
            long branchId,
            [FromQuery] DateTime checkInAt,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] int vehicleCount = 1,
            [FromQuery] bool isParallel = false,
            [FromQuery] int searchDays = 3)
        {
            try
            {
                var suggestion = await _appointmentService.SuggestNearestSlotAsync(
                    branchId,
                    checkInAt,
                    requestedServiceIds,
                    vehicleCount,
                    isParallel,
                    searchDays);
                return Ok(suggestion);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("slot-holds")]
        public async Task<ActionResult<SlotHoldResponseDto>> CreateSlotHold([FromBody] SlotHoldRequestDto dto)
        {
            try
            {
                var result = await _appointmentService.CreateSlotHoldAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("slot-holds/{holdToken}")]
        public async Task<ActionResult> ReleaseSlotHold(string holdToken)
        {
            var released = await _appointmentService.ReleaseSlotHoldAsync(holdToken);
            if (!released)
            {
                return NotFound();
            }
            return NoContent();
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
                return BadRequest(new { message = "Vui lòng cung cấp CustomerId cho luồng guest." });
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

            if (!Enum.TryParse<AppointmentPatchAction>(requested, ignoreCase: true, out var action))
            {
                return BadRequest(new { message = "Unsupported status transition." });
            }

            try
            {
                var actorUserId = this.GetCurrentUserId();
                global::MyGarage.Models.Appointment? appointment = action switch
                {
                    AppointmentPatchAction.CONFIRMED => await _appointmentService.ConfirmAsync(id, actorUserId),
                    AppointmentPatchAction.CANCELED => await _appointmentService.CancelAsync(id, dto?.Reason, actorUserId),
                    AppointmentPatchAction.CHECKED_IN => await _appointmentService.CheckInAsync(id, dto?.ArrivedAt, actorUserId),
                    AppointmentPatchAction.ARRIVED => await _appointmentService.ArriveAsync(id, dto?.ArrivedAt, actorUserId),
                    _ => null
                };

                if (appointment == null)
                {
                    return NotFound();
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
