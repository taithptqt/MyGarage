using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    public partial class AppointmentsController
    {
        [Authorize]
        [HttpGet("{id:long}/edit-capabilities")]
        public async Task<ActionResult<AppointmentEditCapabilityResponseDto>> GetEditCapabilities(long id)
        {
            var result = await _commandFacade.GetEditCapabilitiesAsync(id, BuildActorContext(), HttpContext.RequestAborted);
            return ToTypedActionResult(result);
        }

        [Authorize(Roles = "Staff")]
        [HttpGet("{id:long}/edit-requests")]
        public async Task<IActionResult> GetEditRequests(long id, [FromQuery] string? status = null)
        {
            var result = await _commandFacade.GetEditRequestsAsync(id, status, BuildActorContext(), HttpContext.RequestAborted);
            return ToIActionResult(result);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost("{id:long}/edit-requests")]
        public async Task<IActionResult> RequestEdit(long id, [FromBody] EditRequestCreateDto? dto)
        {
            var result = await _commandFacade.RequestEditAsync(id, dto, BuildActorContext());
            return ToActionResult(result);
        }

        [Authorize]
        [HttpPatch("{id:long}/edit-requests/{requestId:long}")]
        public async Task<IActionResult> DecideEditRequest(long id, long requestId, [FromBody] EditRequestDecisionDto? dto)
        {
            var result = await _commandFacade.DecideEditRequestAsync(id, requestId, dto, BuildActorContext(), HttpContext.RequestAborted);
            return ToActionResult(result);
        }

        private AppointmentActorContext BuildActorContext() => new(
            CurrentUserId: this.GetCurrentUserId(),
            IsStaffUser: this.IsStaffUser(),
            IsAdminStaff: this.IsAdminStaff(),
            CurrentBranchId: this.GetCurrentBranchId(),
            ClaimRole: User.FindFirst(ClaimTypes.Role)?.Value,
            ClaimRoleCode: this.GetCurrentRoleCode(),
            ClaimRoleName: this.GetCurrentRoleName());

        private IActionResult ToActionResult(AppointmentsCommandResult result) =>
            result.StatusCode switch
            {
                200 => result.Payload == null ? Ok() : Ok(result.Payload),
                400 => BadRequest(result.Payload),
                401 => Unauthorized(),
                403 => Forbid(),
                404 => result.Payload == null ? NotFound() : NotFound(result.Payload),
                _ => StatusCode(result.StatusCode, result.Payload)
            };

        private IActionResult ToIActionResult<T>(AppointmentsCommandResult<T> result) =>
            result.StatusCode switch
            {
                200 => Ok(result.Data),
                400 => BadRequest(result.ErrorPayload),
                401 => Unauthorized(),
                403 => Forbid(),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload)
            };

        private ActionResult ToUntypedActionResult<T>(AppointmentsCommandResult<T> result) =>
            result.StatusCode switch
            {
                200 => Ok(result.Data),
                400 => BadRequest(result.ErrorPayload),
                401 => Unauthorized(),
                403 => Forbid(),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload)
            };

        private ActionResult<T> ToTypedActionResult<T>(AppointmentsCommandResult<T> result) =>
            result.StatusCode switch
            {
                200 => Ok(result.Data),
                400 => BadRequest(result.ErrorPayload),
                401 => Unauthorized(),
                403 => Forbid(),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload)
            };
    }
}
