using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/group-sessions")]
    [ApiController]
    [Authorize]
    public class GroupSessionsController : ControllerBase
    {
        private readonly IGroupSessionService _groupSessionService;
        private readonly IAppointmentService _appointmentService;

        public GroupSessionsController(IGroupSessionService groupSessionService, IAppointmentService appointmentService)
        {
            _groupSessionService = groupSessionService;
            _appointmentService = appointmentService;
        }

        [HttpPost]
        public async Task<ActionResult<GroupSessionResponseDto>> Create([FromBody] BookAppointmentBatchRequestDto dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue)
            {
                return Unauthorized();
            }

            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var isCustomer = string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase);
            var customerId = isCustomer ? actorUserId.Value : (dto.CustomerId ?? 0);
            if (customerId <= 0)
            {
                return BadRequest(new { message = "Vui lòng cung cấp CustomerId hợp lệ cho luồng đặt lịch nhóm." });
            }

            try
            {
                var result = await _appointmentService.BookBatchForCustomerAsync(actorUserId.Value, customerId, dto);
                return CreatedAtAction(nameof(GetMyGroupDetail), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("customers/current")]
        public async Task<ActionResult<IEnumerable<GroupSessionResponseDto>>> GetMyGroups()
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var groups = await _groupSessionService.GetByCustomerAsync(customerId.Value);
            return Ok(groups);
        }

        [HttpPost("service-orders")]
        public async Task<ActionResult<GroupSessionResponseDto>> CreateForServiceOrders([FromBody] ServiceOrderGroupSessionCreateDto dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue)
            {
                return Unauthorized();
            }

            if (dto == null || dto.CustomerId <= 0 || dto.BranchId <= 0)
            {
                return BadRequest(new { message = "CustomerId va BranchId phai lon hon 0 de tao GroupSession cho lenh nhom." });
            }

            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId.Value);
            var created = await _groupSessionService.CreateAsync(dto.CustomerId, dto.BranchId, dto.CustomerNote, actorStaffId);
            var result = await _groupSessionService.GetByBranchAndIdAsync(dto.BranchId, created.Id);

            if (result == null)
            {
                return NotFound(new { message = "Da tao GroupSession nhung khong tai duoc thong tin chi tiet." });
            }

            return CreatedAtAction(nameof(GetByBranchDetail), new { branchId = dto.BranchId, id = result.Id }, result);
        }

        [HttpGet("customers/current/{id:long}")]
        public async Task<ActionResult<GroupSessionResponseDto>> GetMyGroupDetail(long id)
        {
            var customerId = this.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Unauthorized();
            }

            var group = await _groupSessionService.GetByCustomerAndIdAsync(customerId.Value, id);
            if (group == null)
            {
                return NotFound();
            }

            return Ok(group);
        }

        [HttpGet("branches/{branchId:long}")]
        public async Task<ActionResult<IEnumerable<GroupSessionResponseDto>>> GetByBranch(long branchId)
        {
            var groups = await _groupSessionService.GetByBranchAsync(branchId);
            return Ok(groups);
        }

        [HttpGet("branches/{branchId:long}/{id:long}")]
        public async Task<ActionResult<GroupSessionResponseDto>> GetByBranchDetail(long branchId, long id)
        {
            var group = await _groupSessionService.GetByBranchAndIdAsync(branchId, id);
            if (group == null)
            {
                return NotFound();
            }

            return Ok(group);
        }

        [HttpPost("{id:long}/cancellations")]
        [HttpPost("{id:long}/cancel-all")]
        public async Task<ActionResult<CancelGroupSessionResponseDto>> CancelAll(long id, [FromBody] CancelGroupSessionRequestDto dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue)
            {
                return Unauthorized();
            }

            var reason = dto?.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Vui lòng nhập lý do hủy nhóm." });
            }

            try
            {
                var result = await _groupSessionService.CancelAllAsync(id, reason, actorUserId.Value);
                if (result == null)
                {
                    return NotFound();
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
