using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [Route("api/v1/appointments")]
    [ApiController]
    public partial class AppointmentsController : ControllerBase
    {
        private const long MaxInsuranceAssetBytes = 10 * 1024 * 1024;

        public sealed record AppointmentStatusPatchRequestDto
        {
            public string? Status { get; init; }
            public string? Reason { get; init; }
            public DateTime? ArrivedAt { get; init; }
        }

        private readonly IAppointmentAvailabilityQueryService _availabilityQueryService;
        private readonly IAppointmentSlotHoldService _slotHoldService;
        private readonly IAppointmentDispatchOpsService _dispatchOpsService;
        private readonly IAppointmentsCommandFacade _commandFacade;
        private readonly IFileSecurityScanner _fileSecurityScanner;

        public AppointmentsController(
            IAppointmentAvailabilityQueryService availabilityQueryService,
            IAppointmentSlotHoldService slotHoldService,
            IAppointmentDispatchOpsService dispatchOpsService,
            IAppointmentsCommandFacade commandFacade,
            IFileSecurityScanner fileSecurityScanner)
        {
            _availabilityQueryService = availabilityQueryService;
            _slotHoldService = slotHoldService;
            _dispatchOpsService = dispatchOpsService;
            _commandFacade = commandFacade;
            _fileSecurityScanner = fileSecurityScanner;
        }

        [AllowAnonymous]
        [HttpGet("slots")]
        public async Task<ActionResult<IEnumerable<AvailableSlotResponseDto>>> GetAvailability(
            [FromQuery] long branchId,
            [FromQuery] DateOnly date,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] string? requestedPackageIds = null,
            [FromQuery] string? bookingFlow = null,
            [FromQuery] int vehicleCount = 1,
            [FromQuery] bool isParallel = true)
        {
            var slots = await _availabilityQueryService.GetAvailableSlotsAsync(branchId, date, requestedServiceIds, requestedPackageIds, bookingFlow, vehicleCount, isParallel);
            return Ok(slots);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/nearest-slot")]
        public async Task<ActionResult<NearestSlotSuggestionResponseDto>> GetNearestSlot(
            long branchId,
            [FromQuery] DateTime checkInAt,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] string? requestedPackageIds = null,
            [FromQuery] string? bookingFlow = null,
            [FromQuery] int vehicleCount = 1,
            [FromQuery] bool isParallel = false,
            [FromQuery] int searchDays = 3)
        {
            try
            {
                var suggestion = await _availabilityQueryService.SuggestNearestSlotAsync(
                    branchId,
                    checkInAt,
                    requestedServiceIds,
                    requestedPackageIds,
                    bookingFlow,
                    vehicleCount,
                    isParallel,
                    searchDays);
                return Ok(suggestion);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [Authorize]
        [HttpPost("slot-holds")]
        public async Task<ActionResult<SlotHoldResponseDto>> CreateSlotHold([FromBody] SlotHoldRequestDto dto)
        {
            try
            {
                var result = await _slotHoldService.CreateSlotHoldAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [Authorize]
        [HttpDelete("slot-holds/{holdToken}")]
        public async Task<ActionResult> ReleaseSlotHold(string holdToken)
        {
            var released = await _slotHoldService.ReleaseSlotHoldAsync(holdToken);
            if (!released)
            {
                return NotFound();
            }
            return NoContent();
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            return Ok(await _commandFacade.GetPagedAsync(scopedBranchId, null, status, page, pageSize, BuildActorContext()));
        }

        [Authorize]
        [HttpGet("customers/current")]
        [HttpGet("~/api/v1/customers/current/appointments")]
        public async Task<ActionResult> GetMine(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null) return Unauthorized();

            return Ok(await _commandFacade.GetPagedAsync(null, customerId.Value, null, page, pageSize, BuildActorContext(), forceCustomerShape: true));
        }

        [Authorize(Roles = "Staff")]
        [HttpGet("branches/{branchId:long}")]
        [HttpGet("~/api/v1/branches/{branchId:long}/appointments")]
        public async Task<ActionResult> GetByBranch(
            long branchId,
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var targetBranchId = scopedBranchId ?? branchId;
            return Ok(await _commandFacade.GetPagedAsync(targetBranchId, null, status, page, pageSize, BuildActorContext()));
        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult> GetById(long id)
        {
            var result = await _commandFacade.GetByIdAsync(id, BuildActorContext(), HttpContext.RequestAborted);
            return ToUntypedActionResult(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] BookAppointmentRequestDto dto)
        {
            var authUserId = this.GetCurrentUserId();
            if (authUserId == null) return Unauthorized();

            var result = await _commandFacade.CreateAsync(authUserId.Value, dto, BuildActorContext());
            if (result.StatusCode == 201 && result.Data != null)
            {
                return CreatedAtAction(nameof(GetById), new { id = result.Data.AppointmentId }, result.Data.Detail);
            }

            return ToUntypedActionResult(result);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost("reception-bookings")]
        public async Task<ActionResult<ReceptionAppointmentSaveResponseDto>> SaveReceptionBooking([FromBody] ReceptionAppointmentSaveRequestDto dto)
        {
            var result = await _commandFacade.SaveReceptionBookingAsync(dto, BuildActorContext());
            return ToTypedActionResult(result);
        }

        [Authorize]
        [HttpPost("insurance-assets")]
        public async Task<ActionResult<object>> UploadInsuranceAsset(IFormFile file, [FromForm] string? assetType)
        {
            if (file == null || file.Length == 0)
            {
                return this.BadRequestProblem("Dữ liệu ảnh bảo hiểm không hợp lệ.");
            }

            if (file.Length > MaxInsuranceAssetBytes)
            {
                return this.BadRequestProblem("Ảnh bảo hiểm vượt quá dung lượng 10MB.");
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequestProblem("Chỉ hỗ trợ tệp ảnh cho hồ sơ bảo hiểm.");
            }

            var scan = await _fileSecurityScanner.ScanFormFileAsync(
                file,
                maxBytes: MaxInsuranceAssetBytes,
                cancellationToken: HttpContext.RequestAborted);
            if (!scan.IsClean)
            {
                return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
            }

            var result = await _commandFacade.UploadInsuranceAssetAsync(
                file.FileName,
                file.ContentType,
                scan.Bytes,
                assetType,
                HttpContext.RequestAborted);
            return ToTypedActionResult(result);
        }

        [AllowAnonymous]
        [HttpPost("public-bookings")]
        public async Task<ActionResult> CreateGuest([FromBody] BookAppointmentRequestDto dto)
        {
            var result = await _commandFacade.CreateGuestAsync(dto);
            if (result.StatusCode == 201 && result.Data != null)
            {
                return CreatedAtAction(nameof(GetById), new { id = result.Data.AppointmentId }, result.Data.Detail);
            }

            return ToUntypedActionResult(result);
        }

        [Authorize]
        [HttpPatch("{id:long}")]
        public async Task<ActionResult> UpdateStatus(long id, [FromBody] AppointmentStatusPatchRequestDto dto)
        {
            var result = await _commandFacade.UpdateStatusAsync(
                id,
                dto?.Status,
                dto?.Reason,
                dto?.ArrivedAt,
                BuildActorContext(),
                HttpContext.RequestAborted);
            return ToUntypedActionResult(result);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/no-show-cycles")]
        public async Task<ActionResult<object>> RunNoShow(long branchId, [FromQuery] DateTime? now = null)
        {
            var affected = await _dispatchOpsService.AutoMarkNoShowsAsync(branchId, now);
            return Ok(new { updated = affected });
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/health-metrics")]
        public async Task<ActionResult<BranchBookingHealthResponseDto>> GetBookingHealth(long branchId, [FromQuery] DateTime? at = null)
        {
            var health = await _dispatchOpsService.GetBranchBookingHealthAsync(branchId, at);
            return Ok(health);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/allocation-metrics")]
        public async Task<ActionResult<DispatchDashboardResponseDto>> GetDispatchDashboard(long branchId, [FromQuery] DateTime? at = null)
        {
            var dashboard = await _dispatchOpsService.GetDispatchDashboardAsync(branchId, at);
            return Ok(dashboard);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/allocation-cycles")]
        public async Task<ActionResult<DispatchCycleResponseDto>> CreateDispatchRun(long branchId, [FromQuery] DateTime? at = null)
        {
            var result = await _dispatchOpsService.RunDispatchCycleAsync(branchId, at);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/overnight-slot-constraints")]
        public async Task<ActionResult<OvernightLockResponseDto>> CreateOvernightLock(long branchId, [FromBody] OvernightLockRequestDto? dto)
        {
            var result = await _dispatchOpsService.LockOvernightSlotsAsync(branchId, dto?.BusinessDate);
            return Ok(result);
        }
    }
}
