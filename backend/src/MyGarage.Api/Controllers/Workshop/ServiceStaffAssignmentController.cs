using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/service-staff-assignments")]
    [ApiController]
    [Authorize]
    public partial class ServiceStaffAssignmentController : ControllerBase
    {
        public sealed class ServiceStaffAssignmentBulkItemDto
        {
            public long? ServiceOrderId { get; set; }
            public long ServiceOrderDetailId { get; set; }
            public long? StaffId { get; set; }
            public long? TechnicianId { get; set; }
            public string? RoleInTask { get; set; }
            public decimal? SplitRatio { get; set; }
            public decimal? EstimatedCommission { get; set; }
            public string? Status { get; set; }
            public bool RequiresInsuranceProof { get; set; }
            public string? ProofImageUrl { get; set; }
            public bool? IsRework { get; set; }
            public long? AssignedBy { get; set; }
        }

        public sealed class ServiceStaffAssignmentBulkRequestDto
        {
            public List<ServiceStaffAssignmentBulkItemDto> Assignments { get; set; } = new();
        }

        public sealed class AssignmentLifecycleRequestDto
        {
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string? Note { get; set; }
            public string? ProofImageUrl { get; set; }
        }

        private readonly IServiceStaffAssignmentService _serviceStaffAssignmentService;
        private readonly IServiceStaffAssignmentQueryDataService _dataService;
        private readonly IServiceStaffAssignmentCommandFacade _commandFacade;

        public ServiceStaffAssignmentController(
            IServiceStaffAssignmentService serviceStaffAssignmentService,
            IServiceStaffAssignmentQueryDataService dataService,
            IServiceStaffAssignmentCommandFacade commandFacade)
        {
            _serviceStaffAssignmentService = serviceStaffAssignmentService;
            _dataService = dataService;
            _commandFacade = commandFacade;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ServiceStaffAssignmentResponseDto>>> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] long? serviceOrderId = null,
            [FromQuery] long? staffId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _dataService.GetPagedAsync(scopedBranchId, serviceOrderId, staffId, normalizedPage, normalizedSize);
            var dtos = _serviceStaffAssignmentService.MapToDtos(paged);
            return Ok(PagedEnvelope<ServiceStaffAssignmentResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var item = await _serviceStaffAssignmentService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && !await _dataService.AssignmentBelongsToBranchAsync(id, scopedBranchId.Value))
            {
                return Forbid();
            }

            return Ok(_serviceStaffAssignmentService.MapToDto(item));
        }

        [HttpPost]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Create([FromBody] ServiceStaffAssignmentCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var result = await _commandFacade.CreateAsync(itemDto, scopedBranchId, this.GetCurrentUserId());
            if (result.StatusCode == 201 && result.Data != null)
            {
                return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
            }

            return ToActionResult(result);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Update(long id, [FromBody] ServiceStaffAssignmentUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var result = await _commandFacade.UpdateAsync(id, itemDto, scopedBranchId);
            return ToActionResult(result);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _commandFacade.DeleteAsync(id, scopedBranchId);
            return ToActionResult(result);
        }

        [HttpPost("multi-assignments")]
        public async Task<ActionResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> AssignMulti(
            [FromBody] ServiceStaffAssignmentMultiCreateDto request)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC", "ADVISOR"))
            {
                return Forbid();
            }

            var result = await _commandFacade.AssignMultiAsync(request, scopedBranchId, this.GetCurrentUserId());
            return ToActionResult(result);
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkAssign([FromBody] ServiceStaffAssignmentBulkRequestDto? request)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC", "ADVISOR"))
            {
                return Forbid();
            }

            var inputs = request?.Assignments?.Select(x => x == null ? null : new ServiceStaffAssignmentBulkItemInput
            {
                ServiceOrderId = x.ServiceOrderId,
                ServiceOrderDetailId = x.ServiceOrderDetailId,
                StaffId = x.StaffId,
                TechnicianId = x.TechnicianId,
                RoleInTask = x.RoleInTask,
                SplitRatio = x.SplitRatio,
                EstimatedCommission = x.EstimatedCommission,
                Status = x.Status,
                RequiresInsuranceProof = x.RequiresInsuranceProof,
                ProofImageUrl = x.ProofImageUrl,
                IsRework = x.IsRework,
                AssignedBy = x.AssignedBy
            }).ToList();

            var result = await _commandFacade.BulkAssignAsync(inputs!, scopedBranchId, this.GetCurrentUserId());
            return result.StatusCode switch
            {
                200 => Ok(result.Data),
                400 => BadRequest(result.ErrorPayload),
                403 => Forbid(),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload)
            };
        }

        [HttpPost("{id:long}/starts")]
        public async Task<IActionResult> StartAssignment(long id, [FromBody] AssignmentLifecycleRequestDto? request = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _commandFacade.StartAssignmentAsync(
                id,
                ToLifecycleInput(request),
                scopedBranchId,
                this.GetCurrentUserId(),
                IsManagerOrForeman());
            return ToActionResult(result).Result!;
        }

        [HttpPost("{id:long}/pauses")]
        public async Task<IActionResult> PauseAssignment(long id, [FromBody] AssignmentLifecycleRequestDto? request = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _commandFacade.PauseAssignmentAsync(
                id,
                ToLifecycleInput(request),
                scopedBranchId,
                this.GetCurrentUserId(),
                IsManagerOrForeman());
            return ToActionResult(result).Result!;
        }

        [HttpPost("{id:long}/completions")]
        public async Task<IActionResult> CompleteAssignment(long id, [FromBody] AssignmentLifecycleRequestDto? request = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _commandFacade.CompleteAssignmentAsync(
                id,
                ToLifecycleInput(request),
                scopedBranchId,
                this.GetCurrentUserId(),
                IsManagerOrForeman());
            return ToActionResult(result).Result!;
        }

        private bool IsManagerOrForeman() =>
            this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC");

        private static ServiceStaffAssignmentLifecycleInput? ToLifecycleInput(AssignmentLifecycleRequestDto? request) =>
            request == null
                ? null
                : new ServiceStaffAssignmentLifecycleInput(request.StartedAt, request.CompletedAt, request.Note, request.ProofImageUrl);

        private IActionResult ToActionResult(ServiceStaffAssignmentCommandResult result) =>
            result.StatusCode switch
            {
                200 => result.Payload == null ? Ok() : Ok(result.Payload),
                204 => NoContent(),
                400 => BadRequest(result.Payload),
                403 => Forbid(),
                404 => result.Payload == null ? NotFound() : NotFound(result.Payload),
                _ => StatusCode(result.StatusCode, result.Payload)
            };

        private ActionResult<T> ToActionResult<T>(ServiceStaffAssignmentCommandResult<T> result) =>
            result.StatusCode switch
            {
                200 => Ok(result.Data),
                400 => BadRequest(result.ErrorPayload),
                403 => Forbid(),
                404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
                _ => StatusCode(result.StatusCode, result.ErrorPayload)
            };
    }
}
