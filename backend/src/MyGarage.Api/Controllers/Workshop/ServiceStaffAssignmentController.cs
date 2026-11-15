using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/service-staff-assignments")]
    [Route("api/v1/service-staff-assignments")]
    [ApiController]
    [Authorize]
    public class ServiceStaffAssignmentController : ControllerBase
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

        private static readonly HashSet<string> AllowedStatuses =
            new(Enum.GetNames<ServiceProgressStatus>(), StringComparer.OrdinalIgnoreCase);

        private readonly IServiceStaffAssignmentService _serviceStaffAssignmentService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IVehicleConditionLogService _vehicleConditionLogService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentController(
            IServiceStaffAssignmentService serviceStaffAssignmentService,
            IServiceOrderService serviceOrderService,
            IVehicleConditionLogService vehicleConditionLogService,
            IGarageContext context,
            IMapper mapper)
        {
            _serviceStaffAssignmentService = serviceStaffAssignmentService;
            _serviceOrderService = serviceOrderService;
            _vehicleConditionLogService = vehicleConditionLogService;
            _context = context;
            _mapper = mapper;
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

            var query = _context.ServiceStaffAssignments.AsNoTracking().AsQueryable();
            if (scopedBranchId.HasValue)
            {
                query = query.Where(a => a.ServiceOrderDetail.ServiceOrder.ExecuteBranchId == scopedBranchId.Value);
            }

            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                query = query.Where(a => a.ServiceOrderDetail.ServiceOrderId == serviceOrderId.Value);
            }

            if (staffId.HasValue && staffId.Value > 0)
            {
                query = query.Where(a => a.TechnicianId == staffId.Value);
            }

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<ServiceStaffAssignment>()
                : await query.OrderByDescending(a => a.Id).Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(paged);
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

            if (scopedBranchId.HasValue && !await AssignmentBelongsToBranchAsync(id, scopedBranchId.Value))
            {
                return Forbid();
            }

            var dto = _mapper.Map<ServiceStaffAssignmentResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Create([FromBody] ServiceStaffAssignmentCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            try
            {
                var statusError = NormalizeAndValidateStatus(itemDto, isCreate: true);
                if (!string.IsNullOrWhiteSpace(statusError))
                {
                    return BadRequest(new { message = statusError });
                }

                var checkInValidationError = await ValidateSignedCheckInBeforeAssignmentAsync(itemDto.ServiceOrderDetailId);
                if (!string.IsNullOrWhiteSpace(checkInValidationError))
                {
                    return BadRequest(new { message = checkInValidationError });
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDetailIdAsync(itemDto.ServiceOrderDetailId);
                if (serviceOrderId <= 0)
                {
                    return BadRequest(new { message = "Không tìm thấy service order tương ứng với hạng mục được phân công." });
                }

                if (scopedBranchId.HasValue && !await ServiceOrderBelongsToBranchAsync(serviceOrderId, scopedBranchId.Value))
                {
                    return Forbid();
                }

                await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

                var entity = _mapper.Map<ServiceStaffAssignment>(itemDto);
                entity.AssignedBy ??= this.GetCurrentUserId();
                entity.RequiresInsuranceProof = await ResolveRequiresInsuranceProofAsync(itemDto.ServiceOrderDetailId, itemDto.RequiresInsuranceProof);
                entity.ProofImageUrl = NormalizeProofImageUrl(itemDto.ProofImageUrl);
                var completionValidationError = ValidateInsuranceProofOnComplete(entity.Status, entity.RequiresInsuranceProof, entity.ProofImageUrl);
                if (!string.IsNullOrWhiteSpace(completionValidationError))
                {
                    return BadRequest(new { message = completionValidationError });
                }
                var createdItem = await _serviceStaffAssignmentService.AddAsync(entity);
                var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("assign-multi")]
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

            try
            {
                var (createdRows, errorResult) = await BuildMultiAssignmentRowsForDetailAsync(request, scopedBranchId);
                if (errorResult != null)
                {
                    return (ActionResult)errorResult;
                }

                _context.ServiceStaffAssignments.AddRange(createdRows!);
                await _context.SaveChangesAsync();

                var response = _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(createdRows!);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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

            if (request == null || request.Assignments == null || request.Assignments.Count == 0)
            {
                return BadRequest(new { message = "Danh sách phân công kỹ thuật viên không được trống." });
            }

            try
            {
                var grouped = request.Assignments
                    .Where(x => x != null)
                    .GroupBy(x => x.ServiceOrderDetailId)
                    .ToList();

                var allCreatedRows = new List<ServiceStaffAssignment>();
                foreach (var group in grouped)
                {
                    var subRequest = new ServiceStaffAssignmentMultiCreateDto
                    {
                        ServiceOrderDetailId = group.Key,
                        AssignedBy = group.Select(x => x.AssignedBy).FirstOrDefault(x => x.HasValue),
                        Assignments = group.Select(x => new ServiceStaffAssignmentMemberDto
                        {
                            TechnicianId = x.TechnicianId ?? x.StaffId ?? 0,
                            RoleInTask = x.RoleInTask,
                            SplitRatio = x.SplitRatio,
                            EstimatedCommission = x.EstimatedCommission,
                            Status = x.Status,
                            RequiresInsuranceProof = x.RequiresInsuranceProof,
                            ProofImageUrl = x.ProofImageUrl,
                            IsRework = x.IsRework
                        }).ToList()
                    };

                    var (rows, errorResult) = await BuildMultiAssignmentRowsForDetailAsync(subRequest, scopedBranchId);
                    if (errorResult != null)
                    {
                        return errorResult;
                    }

                    if (rows != null)
                    {
                        allCreatedRows.AddRange(rows);
                    }
                }

                _context.ServiceStaffAssignments.AddRange(allCreatedRows);
                await _context.SaveChangesAsync();

                var response = _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(allCreatedRows);
                return Ok(new { count = response.Count, items = response });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}/start")]
        public async Task<IActionResult> StartAssignment(long id, [FromBody] AssignmentLifecycleRequestDto? request = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var assignment = await _context.ServiceStaffAssignments
                .Include(a => a.ServiceOrderDetail)
                    .ThenInclude(d => d.ServiceOrder)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null)
            {
                return NotFound(new { message = "Không tìm thấy phân công tác vụ." });
            }

            if (scopedBranchId.HasValue && assignment.ServiceOrderDetail.ServiceOrder.ExecuteBranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            if (!CanOperateAssignment(assignment))
            {
                return Forbid();
            }

            var currentStatus = NormalizeStatus(assignment.Status);
            if (string.IsNullOrWhiteSpace(currentStatus))
            {
                currentStatus = nameof(ServiceProgressStatus.PENDING);
            }

            if (currentStatus is not nameof(ServiceProgressStatus.PENDING) and not nameof(ServiceProgressStatus.PAUSED))
            {
                return BadRequest(new { message = $"Chỉ có thể bắt đầu tác vụ ở trạng thái PENDING hoặc PAUSED (hiện tại: {assignment.Status})." });
            }

            var now = VietnamTime.Now;
            var actorId = this.GetCurrentUserId();

            assignment.ActualStartTime ??= request?.StartedAt?.ToLocalTime() ?? now;
            if (currentStatus == nameof(ServiceProgressStatus.PAUSED))
            {
                var resumeLog = $"[RESUME {now:dd/MM/yyyy HH:mm}]";
                assignment.PauseLog = string.IsNullOrWhiteSpace(assignment.PauseLog)
                    ? resumeLog
                    : $"{assignment.PauseLog}\n{resumeLog}";
            }

            assignment.Status = nameof(ServiceProgressStatus.IN_PROGRESS);
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = actorId;

            var so = assignment.ServiceOrderDetail.ServiceOrder;
            var soStatus = NormalizeStatus(so.Status);
            if (soStatus is nameof(ServiceOrderLifecycleStatus.APPROVED) or nameof(ServiceOrderLifecycleStatus.WAITING) or nameof(ServiceOrderLifecycleStatus.WAITING_PARTS))
            {
                so.Status = nameof(ServiceOrderLifecycleStatus.IN_PROGRESS);
                so.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            return Ok(_mapper.Map<ServiceStaffAssignmentResponseDto>(assignment));
        }

        [HttpPut("{id:long}/pause")]
        public async Task<IActionResult> PauseAssignment(long id, [FromBody] AssignmentLifecycleRequestDto? request = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var assignment = await _context.ServiceStaffAssignments
                .Include(a => a.ServiceOrderDetail)
                    .ThenInclude(d => d.ServiceOrder)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null)
            {
                return NotFound(new { message = "Không tìm thấy phân công tác vụ." });
            }

            if (scopedBranchId.HasValue && assignment.ServiceOrderDetail.ServiceOrder.ExecuteBranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            if (!CanOperateAssignment(assignment))
            {
                return Forbid();
            }

            var currentStatus = NormalizeStatus(assignment.Status);
            if (currentStatus != nameof(ServiceProgressStatus.IN_PROGRESS))
            {
                return BadRequest(new { message = $"Chỉ có thể tạm dừng tác vụ đang IN_PROGRESS (hiện tại: {assignment.Status})." });
            }

            var now = VietnamTime.Now;
            var note = string.IsNullOrWhiteSpace(request?.Note) ? string.Empty : $" - {request.Note.Trim()}";
            var pauseEntry = $"[PAUSE {now:dd/MM/yyyy HH:mm}{note}]";
            assignment.PauseLog = string.IsNullOrWhiteSpace(assignment.PauseLog)
                ? pauseEntry
                : $"{assignment.PauseLog}\n{pauseEntry}";
            assignment.Status = nameof(ServiceProgressStatus.PAUSED);
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = this.GetCurrentUserId();

            await _context.SaveChangesAsync();
            return Ok(_mapper.Map<ServiceStaffAssignmentResponseDto>(assignment));
        }

        [HttpPut("{id:long}/complete")]
        public async Task<IActionResult> CompleteAssignment(long id, [FromBody] AssignmentLifecycleRequestDto? request = null)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var assignment = await _context.ServiceStaffAssignments
                .Include(a => a.ServiceOrderDetail)
                    .ThenInclude(d => d.ServiceOrder)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null)
            {
                return NotFound(new { message = "Không tìm thấy phân công tác vụ." });
            }

            if (scopedBranchId.HasValue && assignment.ServiceOrderDetail.ServiceOrder.ExecuteBranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            if (!CanOperateAssignment(assignment))
            {
                return Forbid();
            }

            var currentStatus = NormalizeStatus(assignment.Status);
            if (currentStatus == nameof(ServiceProgressStatus.COMPLETED))
            {
                return BadRequest(new { message = "Tác vụ này đã hoàn thành trước đó." });
            }

            if (currentStatus is not nameof(ServiceProgressStatus.IN_PROGRESS) and not nameof(ServiceProgressStatus.PAUSED))
            {
                return BadRequest(new { message = $"Cần bắt đầu tác vụ trước khi hoàn thành (hiện tại: {assignment.Status})." });
            }

            var normalizedProof = NormalizeProofImageUrl(request?.ProofImageUrl) ?? assignment.ProofImageUrl;
            var proofError = ValidateInsuranceProofOnComplete(
                nameof(ServiceProgressStatus.COMPLETED),
                assignment.RequiresInsuranceProof,
                normalizedProof);
            if (!string.IsNullOrWhiteSpace(proofError))
            {
                return BadRequest(new { message = proofError });
            }

            var now = VietnamTime.Now;
            assignment.ActualStartTime ??= now;
            assignment.ActualEndTime = request?.CompletedAt?.ToLocalTime() ?? now;
            assignment.ProofImageUrl = normalizedProof;
            assignment.Status = nameof(ServiceProgressStatus.COMPLETED);
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = this.GetCurrentUserId();

            await _context.SaveChangesAsync();
            return Ok(_mapper.Map<ServiceStaffAssignmentResponseDto>(assignment));
        }

        private async Task<(List<ServiceStaffAssignment>? Rows, IActionResult? ErrorResult)> BuildMultiAssignmentRowsForDetailAsync(
            ServiceStaffAssignmentMultiCreateDto request,
            long? scopedBranchId)
        {
            if (request == null || request.ServiceOrderDetailId <= 0 || request.Assignments == null || request.Assignments.Count == 0)
            {
                return (null, BadRequest(new { message = "Thiếu dữ liệu phân công nhiều thợ." }));
            }

            var checkInValidationError = await ValidateSignedCheckInBeforeAssignmentAsync(request.ServiceOrderDetailId);
            if (!string.IsNullOrWhiteSpace(checkInValidationError))
            {
                return (null, BadRequest(new { message = checkInValidationError }));
            }

            var serviceOrderId = await ResolveServiceOrderIdFromDetailIdAsync(request.ServiceOrderDetailId);
            if (serviceOrderId <= 0)
            {
                return (null, BadRequest(new { message = "Không tìm thấy service order tương ứng với hạng mục được phân công." }));
            }

            if (scopedBranchId.HasValue && !await ServiceOrderBelongsToBranchAsync(serviceOrderId, scopedBranchId.Value))
            {
                return (null, Forbid());
            }

            await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

            var detailInfo = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.Id == request.ServiceOrderDetailId)
                .Select(x => new
                {
                    x.ItemName,
                    x.Description,
                    x.PayerType
                })
                .FirstOrDefaultAsync();
            if (detailInfo == null)
            {
                return (null, BadRequest(new { message = "Không tìm thấy hạng mục dịch vụ cần phân công." }));
            }

            var technicianIds = request.Assignments
                .Where(x => x != null && x.TechnicianId > 0)
                .Select(x => x.TechnicianId)
                .Distinct()
                .ToList();
            if (technicianIds.Count == 0)
            {
                return (null, BadRequest(new { message = "Danh sách kỹ thuật viên không hợp lệ." }));
            }

            var duplicateTechnicians = request.Assignments
                .Where(x => x != null && x.TechnicianId > 0)
                .GroupBy(x => x.TechnicianId)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();
            if (duplicateTechnicians.Count > 0)
            {
                return (null, BadRequest(new { message = $"Kỹ thuật viên bị trùng trong cùng hạng mục: {string.Join(", ", duplicateTechnicians)}." }));
            }

            var existingAssignedTechnicianIds = await _context.ServiceStaffAssignments
                .AsNoTracking()
                .Where(x => x.ServiceOrderDetailId == request.ServiceOrderDetailId && technicianIds.Contains(x.TechnicianId))
                .Select(x => x.TechnicianId)
                .Distinct()
                .ToListAsync();
            if (existingAssignedTechnicianIds.Count > 0)
            {
                return (null, BadRequest(new { message = $"Kỹ thuật viên đã được giao trước đó: {string.Join(", ", existingAssignedTechnicianIds)}." }));
            }

            var technicians = await _context.Staffs
                .AsNoTracking()
                .Where(x => technicianIds.Contains(x.Id) && x.IsDeleted != true)
                .Select(x => new
                {
                    x.Id,
                    x.FullName,
                    x.Specialty,
                    x.BranchId
                })
                .ToDictionaryAsync(x => x.Id, x => x);

            var missingTechnicianIds = technicianIds.Where(x => !technicians.ContainsKey(x)).ToList();
            if (missingTechnicianIds.Count > 0)
            {
                return (null, BadRequest(new { message = $"Không tìm thấy kỹ thuật viên: {string.Join(", ", missingTechnicianIds)}." }));
            }

            if (scopedBranchId.HasValue && technicians.Values.Any(t => t.BranchId != scopedBranchId.Value))
            {
                return (null, Forbid());
            }

            var requiredSpecialty = ResolveRequiredSpecialty(detailInfo.ItemName, detailInfo.Description);
            if (!string.Equals(requiredSpecialty, "GENERAL", StringComparison.OrdinalIgnoreCase))
            {
                var invalidSpecialtyRows = new List<string>();
                foreach (var assignment in request.Assignments)
                {
                    var technician = technicians[assignment.TechnicianId];
                    if (!TechnicianSupportsSpecialty(technician.Specialty, requiredSpecialty))
                    {
                        invalidSpecialtyRows.Add($"{technician.FullName}({assignment.TechnicianId})");
                    }
                }

                if (invalidSpecialtyRows.Count > 0)
                {
                    return (null, BadRequest(new
                    {
                        message = $"Chuyên môn kỹ thuật viên không phù hợp hạng mục ({requiredSpecialty}).",
                        invalidTechnicians = invalidSpecialtyRows
                    }));
                }
            }

            var splitRatios = new List<decimal>();
            foreach (var assignment in request.Assignments)
            {
                var ratio = assignment.SplitRatio ?? (1m / request.Assignments.Count);
                if (ratio <= 0m || ratio > 1m)
                {
                    return (null, BadRequest(new { message = $"SplitRatio không hợp lệ cho kỹ thuật viên {assignment.TechnicianId}." }));
                }
                splitRatios.Add(ratio);
            }

            var totalRatio = splitRatios.Sum();
            if (totalRatio > 1.0001m)
            {
                return (null, BadRequest(new { message = $"Tổng SplitRatio vượt quá 1.0 ({totalRatio:0.####})." }));
            }

            var actorId = this.GetCurrentUserId() ?? request.AssignedBy;
            var now = VietnamTime.Now;
            var createdRows = new List<ServiceStaffAssignment>();

            for (var i = 0; i < request.Assignments.Count; i++)
            {
                var item = request.Assignments[i];
                var status = NormalizeStatus(item.Status);
                if (string.IsNullOrWhiteSpace(status))
                {
                    status = nameof(ServiceProgressStatus.PENDING);
                }
                if (!AllowedStatuses.Contains(status))
                {
                    var allowed = string.Join(", ", AllowedStatuses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                    return (null, BadRequest(new { message = $"Giá trị status không hợp lệ: {item.Status}. Chấp nhận: {allowed}." }));
                }

                var proofImageUrl = NormalizeProofImageUrl(item.ProofImageUrl);
                var requiresInsuranceProof = item.RequiresInsuranceProof ||
                    string.Equals(detailInfo.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(detailInfo.PayerType, "SPLIT", StringComparison.OrdinalIgnoreCase);
                var completionValidationError = ValidateInsuranceProofOnComplete(status, requiresInsuranceProof, proofImageUrl);
                if (!string.IsNullOrWhiteSpace(completionValidationError))
                {
                    return (null, BadRequest(new { message = completionValidationError }));
                }

                createdRows.Add(new ServiceStaffAssignment
                {
                    ServiceOrderDetailId = request.ServiceOrderDetailId,
                    AssignedBy = actorId,
                    TechnicianId = item.TechnicianId,
                    RoleInTask = string.IsNullOrWhiteSpace(item.RoleInTask) ? "ASSISTANT" : item.RoleInTask.Trim(),
                    SplitRatio = splitRatios[i],
                    EstimatedCommission = item.EstimatedCommission,
                    Status = status,
                    IsRework = item.IsRework,
                    RequiresInsuranceProof = requiresInsuranceProof,
                    ProofImageUrl = proofImageUrl,
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = actorId
                });
            }

            return (createdRows, null);
        }

        private bool CanOperateAssignment(ServiceStaffAssignment assignment)
        {
            var currentUserId = this.GetCurrentUserId();
            if (currentUserId.HasValue && assignment.TechnicianId == currentUserId.Value)
            {
                return true;
            }

            return this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC");
        }

        private async Task<bool> AssignmentBelongsToBranchAsync(long assignmentId, long branchId)
        {
            return await _context.ServiceStaffAssignments
                .AsNoTracking()
                .AnyAsync(a => a.Id == assignmentId && a.ServiceOrderDetail.ServiceOrder.ExecuteBranchId == branchId);
        }

        private async Task<bool> ServiceOrderBelongsToBranchAsync(long serviceOrderId, long branchId)
        {
            return await _context.ServiceOrders
                .AsNoTracking()
                .AnyAsync(so => so.Id == serviceOrderId && so.ExecuteBranchId == branchId);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Update(long id, [FromBody] ServiceStaffAssignmentUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            try
            {
                var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (scopedBranchId.HasValue && !await AssignmentBelongsToBranchAsync(id, scopedBranchId.Value))
                {
                    return Forbid();
                }

                var statusError = NormalizeAndValidateStatus(itemDto, isCreate: false);
                if (!string.IsNullOrWhiteSpace(statusError))
                {
                    return BadRequest(new { message = statusError });
                }

                var checkInValidationError = await ValidateSignedCheckInBeforeAssignmentAsync(itemDto.ServiceOrderDetailId);
                if (!string.IsNullOrWhiteSpace(checkInValidationError))
                {
                    return BadRequest(new { message = checkInValidationError });
                }

                var detailId = itemDto.ServiceOrderDetailId > 0
                    ? itemDto.ServiceOrderDetailId
                    : existingItem.ServiceOrderDetailId;
                var serviceOrderId = await ResolveServiceOrderIdFromDetailIdAsync(detailId);
                if (serviceOrderId <= 0)
                {
                    return BadRequest(new { message = "Không tìm thấy service order tương ứng với hạng mục được phân công." });
                }

                if (scopedBranchId.HasValue && !await ServiceOrderBelongsToBranchAsync(serviceOrderId, scopedBranchId.Value))
                {
                    return Forbid();
                }

                await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

                _mapper.Map(itemDto, existingItem);
                existingItem.RequiresInsuranceProof = await ResolveRequiresInsuranceProofAsync(detailId, itemDto.RequiresInsuranceProof);
                existingItem.ProofImageUrl = NormalizeProofImageUrl(itemDto.ProofImageUrl);
                var completionValidationError = ValidateInsuranceProofOnComplete(existingItem.Status, existingItem.RequiresInsuranceProof, existingItem.ProofImageUrl);
                if (!string.IsNullOrWhiteSpace(completionValidationError))
                {
                    return BadRequest(new { message = completionValidationError });
                }
                var updatedItem = await _serviceStaffAssignmentService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            try
            {
                var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (scopedBranchId.HasValue && !await AssignmentBelongsToBranchAsync(id, scopedBranchId.Value))
                {
                    return Forbid();
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDetailIdAsync(existingItem.ServiceOrderDetailId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);
                }

                var result = await _serviceStaffAssignmentService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private static string? NormalizeAndValidateStatus(ServiceStaffAssignmentCreateDto dto, bool isCreate)
        {
            if (dto == null)
            {
                return "Dữ liệu phân công kỹ thuật không hợp lệ.";
            }

            var normalized = NormalizeStatus(dto.Status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (isCreate)
                {
                    dto.Status = nameof(ServiceProgressStatus.PENDING);
                }
                return null;
            }

            if (!AllowedStatuses.Contains(normalized))
            {
                var allowed = string.Join(", ", AllowedStatuses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị status không hợp lệ: {dto.Status}. Chấp nhận: {allowed}.";
            }

            dto.Status = normalized;
            return null;
        }

        private static string? NormalizeAndValidateStatus(ServiceStaffAssignmentUpdateDto dto, bool isCreate)
        {
            if (dto == null)
            {
                return "Dữ liệu phân công kỹ thuật không hợp lệ.";
            }

            var normalized = NormalizeStatus(dto.Status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (isCreate)
                {
                    dto.Status = nameof(ServiceProgressStatus.PENDING);
                }
                return null;
            }

            if (!AllowedStatuses.Contains(normalized))
            {
                var allowed = string.Join(", ", AllowedStatuses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị status không hợp lệ: {dto.Status}. Chấp nhận: {allowed}.";
            }

            dto.Status = normalized;
            return null;
        }

        private static string NormalizeStatus(string? status)
            => string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();

        private static string? NormalizeProofImageUrl(string? value)
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            return trimmed.Length <= 500 ? trimmed : trimmed[..500];
        }

        private static string? ValidateInsuranceProofOnComplete(string? status, bool requiresInsuranceProof, string? proofImageUrl)
        {
            if (!requiresInsuranceProof)
            {
                return null;
            }

            if (!string.Equals(NormalizeStatus(status), nameof(ServiceProgressStatus.COMPLETED), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(proofImageUrl)
                ? "Hạng mục bảo hiểm bắt buộc ảnh nghiệm thu trước khi hoàn thành."
                : null;
        }

        private async Task<string?> ValidateSignedCheckInBeforeAssignmentAsync(long serviceOrderDetailId)
        {
            if (serviceOrderDetailId <= 0)
            {
                return "Thiếu ServiceOrderDetailId để phân công kỹ thuật viên.";
            }

            var serviceOrderId = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.Id == serviceOrderDetailId)
                .Select(x => x.ServiceOrderId)
                .FirstOrDefaultAsync();

            if (serviceOrderId <= 0)
            {
                return "Không tìm thấy hạng mục dịch vụ của lệnh sửa chữa.";
            }

            var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(serviceOrderId);
            if (!hasSignedCheckIn)
            {
                return "Xe chưa hoàn tất check-in có chữ ký khách hàng. Vui lòng vào Nhật ký xe để hoàn tất trước khi giao việc cho thợ.";
            }

            return null;
        }

        private async Task<long> ResolveServiceOrderIdFromDetailIdAsync(long serviceOrderDetailId)
        {
            if (serviceOrderDetailId <= 0)
            {
                return 0;
            }

            return await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.Id == serviceOrderDetailId)
                .Select(x => x.ServiceOrderId)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> ResolveRequiresInsuranceProofAsync(long serviceOrderDetailId, bool explicitFlag)
        {
            if (explicitFlag)
            {
                return true;
            }

            if (serviceOrderDetailId <= 0)
            {
                return false;
            }

            var payerType = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.Id == serviceOrderDetailId)
                .Select(x => x.PayerType)
                .FirstOrDefaultAsync();

            return string.Equals(payerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(payerType, "SPLIT", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRequiredSpecialty(string? itemName, string? description)
        {
            var source = CompactSearchText($"{itemName} {description}");
            if (ContainsAny(source, "SON", "DONG", "VO", "PAINT", "BODY"))
            {
                return "PAINTING";
            }

            if (ContainsAny(source, "DIEN", "ELECTRIC", "ECU", "CAMBIEN", "SENSOR", "MAYLANH", "AC"))
            {
                return "ELECTRICAL";
            }

            if (ContainsAny(source, "GAM", "CHASSIS", "PHANH", "TREO", "LAI", "BANH", "LOP"))
            {
                return "CHASSIS";
            }

            if (ContainsAny(source, "MAY", "ENGINE", "DONGCO", "XECMANG", "HOPSO"))
            {
                return "ENGINE";
            }

            return "GENERAL";
        }

        private static bool TechnicianSupportsSpecialty(string? technicianSpecialty, string requiredSpecialty)
        {
            if (string.IsNullOrWhiteSpace(requiredSpecialty) ||
                string.Equals(requiredSpecialty, "GENERAL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var source = CompactSearchText(technicianSpecialty);
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (ContainsAny(source, "GENERAL", "DANANG", "DANANG", "UNIVERSAL", "ALL"))
            {
                return true;
            }

            var required = CompactSearchText(requiredSpecialty);
            return source.Contains(required, StringComparison.Ordinal);
        }

        private static string CompactSearchText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            return builder
                .ToString()
                .Replace('Đ', 'D')
                .Replace('đ', 'D')
                .Normalize(NormalizationForm.FormC);
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (var token in tokens)
            {
                var normalized = CompactSearchText(token);
                if (source.Contains(normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}






