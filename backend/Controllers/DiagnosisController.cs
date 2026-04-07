using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/diagnoses")]
    [ApiController]
    public class DiagnosisController : ControllerBase
    {
        private const string DiagnosisStatusPending = nameof(DiagnosisStatus.PENDING);
        private const string DiagnosisStatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
        private const string DiagnosisStatusCompleted = nameof(DiagnosisStatus.COMPLETED);
        private const string ServiceOrderStatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);

        public sealed record DiagnosisStatusPatchDto
        {
            public string? Status { get; init; }
            public string? Symptom { get; init; }
            public string? RootCause { get; init; }
            public string? Severity { get; init; }
        }

        private readonly IDiagnosisService _diagnosisService;
        private readonly IMapper _mapper;

        public DiagnosisController(IDiagnosisService diagnosisService, IMapper mapper)
        {
            _diagnosisService = diagnosisService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll(
            [FromQuery] long? serviceOrderId,
            [FromQuery] bool requestOnly = false,
            [FromQuery] long? teamId = null)
        {
            var items = await _diagnosisService.GetAllFilteredAsync(serviceOrderId, requestOnly, teamId);
            var dtos = _mapper.Map<IEnumerable<DiagnosisResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("dispatch-board")]
        public async Task<ActionResult<IEnumerable<DiagnosisDispatchBoardItemDto>>> GetDispatchBoard(
            [FromQuery] long? branchId = null,
            [FromQuery] long? teamId = null,
            [FromQuery] bool includeCompleted = false)
        {
            var board = await _diagnosisService.GetDispatchBoardAsync(branchId, teamId, includeCompleted);
            return Ok(board);
        }

        [HttpGet("service-orders/{serviceOrderId:long}/dispatch-suggestions")]
        public async Task<ActionResult<DiagnosisDispatchSuggestionDto>> GetDispatchSuggestion(
            long serviceOrderId,
            [FromQuery] long teamId)
        {
            if (teamId <= 0)
            {
                return BadRequest(new { message = "Bat buoc cung cap teamId de lay goi y phan cong." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var suggestion = await _diagnosisService.BuildDispatchSuggestionAsync(serviceOrderId, teamId);
            return Ok(suggestion);
        }

        [HttpGet("service-orders/{serviceOrderId:long}/intake-suggestions")]
        public async Task<ActionResult<DiagnosisIntakeSuggestionDto>> GetIntakeSuggestion(
            long serviceOrderId,
            [FromQuery] string? dispatchTag = null,
            [FromQuery] int? bufferMinutes = null,
            [FromQuery] string? intakePackageCode = null)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var suggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
                serviceOrderId,
                NormalizeDispatchTag(dispatchTag),
                bufferMinutes,
                intakePackageCode);

            return Ok(suggestion);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/handover-snapshots")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> CreateHandoverSnapshot(
            long serviceOrderId,
            [FromBody] DiagnosisHandoverSnapshotCreateDto? request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Du lieu T2 khong hop le." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var snapshot = await _diagnosisService.CreateHandoverSnapshotAsync(serviceOrderId, request);
            return Ok(snapshot);
        }

        [HttpGet("service-orders/{serviceOrderId:long}/handover-snapshots/latest")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> GetLatestHandoverSnapshot(long serviceOrderId)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var snapshot = await _diagnosisService.GetLatestHandoverSnapshotAsync(serviceOrderId);
            if (snapshot == null)
            {
                return NotFound(new { message = "Chua co snapshot T2 sau chan doan." });
            }

            return Ok(snapshot);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/handover-snapshots/{snapshotId:long}/customer-decisions")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> ApplyHandoverCustomerDecision(
            long serviceOrderId,
            long snapshotId,
            [FromBody] DiagnosisHandoverDecisionCreateDto? request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Thieu thong tin duyet/Tu choi cua khach." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            try
            {
                var result = await _diagnosisService.ApplyHandoverCustomerDecisionAsync(serviceOrderId, snapshotId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("kpis/intake-conversions")]
        public async Task<ActionResult<DiagnosisIntakeKpiResponseDto>> GetIntakeConversionKpi(
            [FromQuery] long? branchId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var result = await _diagnosisService.GetIntakeConversionKpiAsync(branchId, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> GetById(long id)
        {
            var item = await _diagnosisService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<DiagnosisResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DiagnosisResponseDto>> Create([FromBody] DiagnosisCreateDto itemDto)
        {
            if (itemDto == null)
            {
                return BadRequest(new { message = "Du lieu chan doan khong hop le." });
            }

            itemDto.Status = NormalizeStatus(itemDto.Status);
            itemDto.DispatchTag = NormalizeDispatchTag(itemDto.DispatchTag);

            var entity = _mapper.Map<Diagnosis>(itemDto);
            var createdItem = await _diagnosisService.AddAsync(entity);
            var responseDto = _mapper.Map<DiagnosisResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> Update(long id, [FromBody] DiagnosisUpdateDto itemDto)
        {
            var existingItem = await _diagnosisService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var status = NormalizeStatus(itemDto.Status ?? existingItem.Status ?? DiagnosisStatusPending);

            _mapper.Map(itemDto, existingItem);
            existingItem.Status = status;
            existingItem.DispatchTag = NormalizeDispatchTag(existingItem.DispatchTag);
            existingItem.UpdatedAt = DateTime.UtcNow;

            var updatedItem = await _diagnosisService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<DiagnosisResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<ActionResult<DiagnosisResponseDto>> RequestDiagnosis(long serviceOrderId, [FromBody] DiagnosisQueueCreateRequestDto? request)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var hasSignedCheckIn = await _diagnosisService.HasSignedCheckInAsync(serviceOrderId);
            if (!hasSignedCheckIn)
            {
                return BadRequest(new { message = "Chi duoc yeu cau chan doan sau khi khach da ky check-in." });
            }

            var allDiagnosis = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            if (allDiagnosis.Any(x => x.Status == DiagnosisStatusCompleted))
            {
                return BadRequest(new { message = "Lenh nay da co ket qua chan doan hoan tat." });
            }

            var activeDiagnosis = allDiagnosis.Where(x => x.Status != DiagnosisStatusCompleted).ToList();
            if (activeDiagnosis.Count > 0)
            {
                var activeIds = activeDiagnosis.Select(x => x.Id).ToList();
                await _diagnosisService.RemoveRecommendationsByDiagnosisIdsAsync(activeIds);
                await _diagnosisService.RemoveDiagnosesAsync(activeDiagnosis);
            }

            var symptoms = ExtractSymptomsFromNotes(serviceOrder.CustomerNotes);
            var dispatchTag = NormalizeDispatchTag(request?.DispatchTag);
            DiagnosisIntakeSuggestionDto? intakeSuggestion = null;
            var shouldBuildIntakeSuggestion =
                (request?.AutoAssign ?? false) ||
                (request?.EnforceCapacityGuard ?? false) ||
                !string.IsNullOrWhiteSpace(request?.IntakePackageCode);

            if (shouldBuildIntakeSuggestion)
            {
                intakeSuggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
                    serviceOrderId,
                    dispatchTag,
                    request?.BufferMinutes,
                    request?.IntakePackageCode);

                var bypassConflictGuard = request?.BypassConflictGuard ?? false;
                if (!intakeSuggestion.CanApply && !bypassConflictGuard)
                {
                    return Conflict(new
                    {
                        message = "Khung gio bi xung dot lich booking. Bat bypass de tiep tuc.",
                        conflictLevel = intakeSuggestion.ConflictLevel,
                        conflictMinutes = intakeSuggestion.ConflictMinutes,
                        finalPackageCode = intakeSuggestion.FinalPackageCode,
                        t1EstimatedHandover = intakeSuggestion.T1EstimatedHandover
                    });
                }

                if (intakeSuggestion.RequiresCustomerConsent && request?.CustomerConsentAccepted != true)
                {
                    return BadRequest(new
                    {
                        message = "Goi chan doan 30p/60p can xac nhan dong y cua khach truoc khi tao yeu cau."
                    });
                }
            }

            var placeholderTechnicianId = await _diagnosisService.ResolvePlaceholderTechnicianIdAsync(serviceOrder, dispatchTag);
            var newDiagnosis = await _diagnosisService.CreatePendingDiagnosesAsync(serviceOrderId, placeholderTechnicianId, symptoms, dispatchTag);

            if ((request?.AutoAssign ?? false) && intakeSuggestion != null && intakeSuggestion.SuggestedLeadTechnicianId.HasValue)
            {
                var resolvedTeamId = intakeSuggestion.SuggestedTeamId;
                if (!resolvedTeamId.HasValue)
                {
                    resolvedTeamId = await _diagnosisService.ResolveTeamIdForTechnicianAsync(intakeSuggestion.SuggestedLeadTechnicianId.Value);
                }

                await _diagnosisService.AssignDiagnosesAsync(
                    newDiagnosis,
                    intakeSuggestion.SuggestedLeadTechnicianId.Value,
                    assistantTechnicianId: intakeSuggestion.SuggestedAssistantTechnicianId,
                    teamId: resolvedTeamId,
                    supportingTeamIds: null,
                    supportingAssignments: null,
                    bayId: intakeSuggestion.SuggestedBayId,
                    managerOverride: false);
            }

            if (intakeSuggestion != null)
            {
                serviceOrder.EstimatedHandover = intakeSuggestion.T1EstimatedHandover;
                await _diagnosisService.RecordIntakeSnapshotAsync(
                    serviceOrder,
                    intakeSuggestion,
                    dispatchTag,
                    bypassConflictGuard: request?.BypassConflictGuard ?? false,
                    autoAssign: request?.AutoAssign ?? false,
                    customerConsentAccepted: request?.CustomerConsentAccepted == true,
                    customerConsentChannel: request?.CustomerConsentChannel,
                    customerConsentNote: request?.CustomerConsentNote,
                    diagnoses: newDiagnosis);
            }

            await _diagnosisService.UpdateServiceOrderStatusAsync(serviceOrder, ServiceOrderStatusDiagnosing);

            var dto = _mapper.Map<DiagnosisResponseDto>(newDiagnosis.First());
            return Ok(dto);
        }

        [HttpPatch("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchRequestAssignment(long serviceOrderId, [FromBody] DiagnosisAcceptRequestDto request)
        {
            return await AssignRequestAsync(serviceOrderId, request);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/start")]
        public async Task<ActionResult<DiagnosisResponseDto>> StartDiagnosis(long serviceOrderId, [FromBody] DiagnosisStartRequestDto request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin ky thuat vien bat dau chan doan." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Khong co lenh chan doan dang hoat dong." });
            }

            var isAssignedMember = activeDiagnosis.Any(x =>
                x.TechnicianId == request.TechnicianId ||
                (x.AssistantTechnicianId.HasValue && x.AssistantTechnicianId.Value == request.TechnicianId));
            if (!isAssignedMember)
            {
                return Forbid();
            }

            if (request.BayId.HasValue && serviceOrder.ExecuteBranchId > 0)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return BadRequest(new { message = "Cau nang duoc chon khong thuoc chi nhanh dang xu ly." });
                }
            }

            await _diagnosisService.StartDiagnosesAsync(activeDiagnosis, request.TechnicianId, request.BayId);
            await _diagnosisService.UpdateServiceOrderStatusAsync(serviceOrder, ServiceOrderStatusDiagnosing);

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            return Ok(_mapper.Map<DiagnosisResponseDto>(response));
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-approval")]
        public async Task<IActionResult> ApproveTechnical(long serviceOrderId, [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin to truong duyet ky thuat." });
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return BadRequest(new { message = "To truong khong ton tai." });
            }

            var diagnoses = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return BadRequest(new { message = "Khong co ket qua chan doan de duyet." });
            }

            if (diagnoses.Any(x => !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { message = "Chi duoc duyet ky thuat sau khi toan bo trieu chung da o trang thai COMPLETED." });
            }

            await _diagnosisService.ApproveTechnicalAsync(diagnoses, request.TeamLeaderId, request.Note);
            return Ok(new { message = "Da duyet bao cao ky thuat." });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/final-approval")]
        public async Task<IActionResult> ApproveFinal(long serviceOrderId, [FromBody] DiagnosisFinalApprovalRequestDto request)
        {
            if (request == null || request.ManagerId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin quan doc duyet bao gia." });
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return BadRequest(new { message = "Quan doc khong ton tai." });
            }

            var diagnoses = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return BadRequest(new { message = "Khong co du lieu chan doan de duyet." });
            }

            if (diagnoses.Any(x => !x.ApprovedByForemanId.HasValue))
            {
                return BadRequest(new { message = "Can to truong duyet bao cao ky thuat truoc khi quan doc duyet bao gia." });
            }

            await _diagnosisService.ApproveFinalAsync(diagnoses, request.ManagerId, request.Note);
            return Ok(new { message = "Da duyet bao gia cuoi cung va tra ket qua ve le tan." });
        }

        [HttpDelete("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<IActionResult> DeleteRequest(long serviceOrderId)
        {
            return await DeleteRequestAsync(serviceOrderId);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/support")]
        public async Task<IActionResult> RequestSupport(long serviceOrderId, [FromBody] DiagnosisSupportRequestDto request)
        {
            if (request == null || request.RequestedByStaffId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin nguoi yeu cau ho tro." });
            }

            var requester = await _diagnosisService.GetStaffByIdAsync(request.RequestedByStaffId);
            if (requester == null)
            {
                return BadRequest(new { message = "Nguoi yeu cau ho tro khong ton tai." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Khong co lenh chan doan dang hoat dong de yeu cau ho tro." });
            }

            var primaryTeamId = activeDiagnosis
                .Where(x => x.AssignedTeamId.HasValue && x.AssignedTeamId.Value > 0)
                .Select(x => x.AssignedTeamId!.Value)
                .FirstOrDefault();
            if (primaryTeamId <= 0)
            {
                primaryTeamId = null;
            }

            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                teamIds: null,
                assignments: request.SupportingAssignments,
                primaryTeamId: primaryTeamId);

            if (normalizedSupportingAssignments.Count > 0 && !primaryTeamId.HasValue)
            {
                return BadRequest(new { message = "Chi cho phep yeu cau to phoi hop sau khi da co to chinh." });
            }

            if (normalizedSupportingAssignments.Count > 0)
            {
                var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return NotFound(new { message = "Khong tim thay lenh sua chua." });
                }

                var dispatchTag = activeDiagnosis
                    .Select(x => x.DispatchTag)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.RequestedTechnicianCount.GetValueOrDefault() <= 0)
                    {
                        return BadRequest(new { message = "Moi to phoi hop phai co so luong tho yeu cau lon hon 0." });
                    }

                    if (supportingAssignment.TechnicianId > 0 || supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        return BadRequest(new { message = "To truong to chinh khong duoc chon tho cho to phoi hop." });
                    }

                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingAssignment.TeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return BadRequest(new { message = $"To phoi hop #{supportingAssignment.TeamId} khong ton tai." });
                    }

                    if (supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return BadRequest(new { message = "To phoi hop phai thuoc cung chi nhanh voi lenh sua chua." });
                    }

                    var isCompatible = await _diagnosisService.IsDispatchTagCompatibleAsync(dispatchTag, supportingAssignment.TeamId);
                    if (!isCompatible)
                    {
                        return Conflict(new { message = "Co to phoi hop khong dung chuyen mon cua lenh chan doan." });
                    }
                }
            }

            await _diagnosisService.MarkSupportRequestedAsync(
                activeDiagnosis,
                request.RequestedByStaffId,
                request.Note,
                normalizedSupportingAssignments);
            return Ok(new
            {
                message = "Da ghi nhan yeu cau ho tro ky thuat.",
                serviceOrderId,
                requestedBy = request.RequestedByStaffId
            });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/cancel-requests")]
        public async Task<IActionResult> RequestCancelAssignment(long serviceOrderId, [FromBody] DiagnosisCancelRequestDto request)
        {
            if (request == null || request.RequestedByStaffId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin nguoi yeu cau huy." });
            }

            var reason = (request.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Bat buoc nhap ly do huy (Sai chuyen mon, Qua tai cau, Thieu thiet bi...)." });
            }

            var requester = await _diagnosisService.GetStaffByIdAsync(request.RequestedByStaffId);
            if (requester == null)
            {
                return BadRequest(new { message = "Nguoi yeu cau huy khong ton tai." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Khong co lenh chan doan dang hoat dong de huy." });
            }

            try
            {
                await _diagnosisService.RequestCancellationAsync(activeDiagnosis, request.RequestedByStaffId, reason);
                return Ok(new { message = "Da gui lenh huy sang trang thai cho duyet cua quan doc." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/cancel-requests/decision")]
        public async Task<IActionResult> DecideCancelAssignment(long serviceOrderId, [FromBody] DiagnosisCancelDecisionDto request)
        {
            if (request == null || request.ManagerId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin quan doc phe duyet." });
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return BadRequest(new { message = "Quan doc khong ton tai." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var allDiagnosis = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            var pendingCancellation = allDiagnosis
                .Where(x => x.CancellationRequestedAt.HasValue && !x.CancellationDecisionAt.HasValue)
                .ToList();

            if (pendingCancellation.Count == 0)
            {
                return BadRequest(new { message = "Khong co yeu cau huy nao dang cho duyet." });
            }

            await _diagnosisService.ApplyCancellationDecisionAsync(serviceOrder, pendingCancellation, request.Approve, request.ManagerId, request.Note);

            return Ok(new
            {
                message = request.Approve
                    ? "Quan doc da dong y huy, lenh duoc dua ve hang cho phan cong lai."
                    : "Quan doc tu choi huy lenh, to tiep tuc xu ly xe.",
                approved = request.Approve
            });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/manager-override")]
        public async Task<ActionResult<DiagnosisResponseDto>> ManagerOverrideAssignment(long serviceOrderId, [FromBody] DiagnosisManagerOverrideDto request)
        {
            if (request == null || request.ManagerId <= 0 || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin override cua quan doc." });
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return BadRequest(new { message = "Tho phu khong duoc trung voi tho chinh." });
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return BadRequest(new { message = "Quan doc khong ton tai." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Khong co lenh chan doan de override." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var technicianExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!technicianExists)
            {
                return BadRequest(new { message = "Ky thuat vien khong ton tai." });
            }

            var resolvedTeamId = request.TeamId ?? await _diagnosisService.ResolveTeamIdForTechnicianAsync(request.TechnicianId);
            if (request.TeamId.HasValue)
            {
                var inTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId.Value);
                if (!inTeam)
                {
                    return BadRequest(new { message = "Ky thuat vien khong thuoc to duoc chi dinh." });
                }
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return BadRequest(new { message = "Tho phu khong ton tai." });
                }

                if (resolvedTeamId.HasValue)
                {
                    var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, resolvedTeamId.Value);
                    if (!assistantInTeam)
                    {
                        return BadRequest(new { message = "Tho phu khong thuoc to duoc giao." });
                    }
                }
            }

            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                request.SupportingTeamIds,
                request.SupportingAssignments,
                resolvedTeamId);
            var normalizedSupportingTeamIds = normalizedSupportingAssignments
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            if (normalizedSupportingTeamIds.Count > 0 && !resolvedTeamId.HasValue)
            {
                return BadRequest(new { message = "Khi co to phoi hop, bat buoc chi dinh to chinh." });
            }
            if (normalizedSupportingTeamIds.Count > 0)
            {
                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.TechnicianId <= 0)
                    {
                        return BadRequest(new { message = "Moi to phoi hop phai chi dinh tho chinh." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue && supportingAssignment.AssistantTechnicianId.Value == supportingAssignment.TechnicianId)
                    {
                        return BadRequest(new { message = "Tho phu cua to phoi hop khong duoc trung tho chinh." });
                    }

                    var supportingTeamId = supportingAssignment.TeamId;
                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingTeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return BadRequest(new { message = $"To phoi hop #{supportingTeamId} khong ton tai." });
                    }

                    if (supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return BadRequest(new { message = "To phoi hop phai thuoc cung chi nhanh voi lenh sua chua." });
                    }

                    var supportingLeadExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.TechnicianId);
                    if (!supportingLeadExists)
                    {
                        return BadRequest(new { message = $"Tho chinh cua to phoi hop #{supportingTeamId} khong ton tai." });
                    }

                    var supportingLeadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.TechnicianId, supportingTeamId);
                    if (!supportingLeadInTeam)
                    {
                        return BadRequest(new { message = $"Tho chinh cua to phoi hop #{supportingTeamId} khong thuoc to duoc chon." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        var supportingAssistantExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.AssistantTechnicianId.Value);
                        if (!supportingAssistantExists)
                        {
                            return BadRequest(new { message = $"Tho phu cua to phoi hop #{supportingTeamId} khong ton tai." });
                        }

                        var supportingAssistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.AssistantTechnicianId.Value, supportingTeamId);
                        if (!supportingAssistantInTeam)
                        {
                            return BadRequest(new { message = $"Tho phu cua to phoi hop #{supportingTeamId} khong thuoc to duoc chon." });
                        }
                    }
                }
            }

            if (request.BayId.HasValue)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return BadRequest(new { message = "Cau nang khong thuoc chi nhanh xu ly." });
                }
            }

            await _diagnosisService.AssignDiagnosesAsync(
                activeDiagnosis,
                request.TechnicianId,
                assistantTechnicianId: request.AssistantTechnicianId,
                teamId: resolvedTeamId,
                supportingTeamIds: normalizedSupportingTeamIds,
                supportingAssignments: normalizedSupportingAssignments,
                bayId: request.BayId,
                managerOverride: true);

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();

            var dto = _mapper.Map<DiagnosisResponseDto>(response);
            return Ok(dto);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/supporting-team-selections")]
        public async Task<ActionResult<DiagnosisResponseDto>> SelectSupportingTeamAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisSupportingTeamSelectionRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0 || request.TeamId <= 0 || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thieu thong tin chon tho ho tro cua to phoi hop." });
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return BadRequest(new { message = "Tho phu khong duoc trung voi tho chinh." });
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return BadRequest(new { message = "To truong khong ton tai." });
            }

            var teamLeaderTeamId = teamLeader.TeamId;
            if (!teamLeaderTeamId.HasValue || teamLeaderTeamId.Value != request.TeamId)
            {
                return BadRequest(new { message = "To truong chi duoc chon nhan su cho to cua minh." });
            }

            var leadExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!leadExists)
            {
                return BadRequest(new { message = "Tho chinh khong ton tai." });
            }

            var leadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId);
            if (!leadInTeam)
            {
                return BadRequest(new { message = "Tho chinh khong thuoc to duoc yeu cau." });
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return BadRequest(new { message = "Tho phu khong ton tai." });
                }

                var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, request.TeamId);
                if (!assistantInTeam)
                {
                    return BadRequest(new { message = "Tho phu khong thuoc to duoc yeu cau." });
                }
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Khong co lenh chan doan dang hoat dong de cap nhat nhan su ho tro." });
            }

            await _diagnosisService.AssignSupportingTeamSelectionAsync(
                activeDiagnosis,
                request.TeamId,
                request.TeamLeaderId,
                request.TechnicianId,
                request.AssistantTechnicianId,
                request.Note);

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            return Ok(_mapper.Map<DiagnosisResponseDto>(response));
        }

        [HttpPatch("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchStatus(long id, [FromBody] DiagnosisStatusPatchDto dto)
        {
            var status = NormalizeStatus(dto?.Status);
            if (status != DiagnosisStatusCompleted)
            {
                return BadRequest(new { message = "Chi ho tro chuyen trang thai sang COMPLETED." });
            }

            var payload = new DiagnosisUpdateDto
            {
                Status = DiagnosisStatusCompleted,
                Symptom = dto?.Symptom,
                RootCause = dto?.RootCause,
                Severity = dto?.Severity,
            };

            return await CompleteAsync(id, payload);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _diagnosisService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private static string NormalizeStatus(string? rawStatus)
        {
            var normalized = (rawStatus ?? string.Empty).Trim().ToUpperInvariant();
            return Enum.TryParse<DiagnosisStatus>(normalized, true, out var parsed)
                ? parsed.ToString()
                : DiagnosisStatusPending;
        }

        private static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
        }

        private static IReadOnlyList<DiagnosisSupportingTeamAssignmentDto> NormalizeSupportingAssignments(
            IEnumerable<long>? teamIds,
            IEnumerable<DiagnosisSupportingTeamAssignmentDto>? assignments,
            long? primaryTeamId)
        {
            var byTeam = new Dictionary<long, DiagnosisSupportingTeamAssignmentDto>();

            foreach (var teamId in teamIds ?? Array.Empty<long>())
            {
                if (teamId <= 0)
                {
                    continue;
                }

                if (primaryTeamId.HasValue && teamId == primaryTeamId.Value)
                {
                    continue;
                }

                byTeam[teamId] = new DiagnosisSupportingTeamAssignmentDto
                {
                    TeamId = teamId,
                    TechnicianId = 0,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = null
                };
            }

            foreach (var assignment in assignments ?? Array.Empty<DiagnosisSupportingTeamAssignmentDto>())
            {
                if (assignment == null || assignment.TeamId <= 0)
                {
                    continue;
                }

                if (primaryTeamId.HasValue && assignment.TeamId == primaryTeamId.Value)
                {
                    continue;
                }

                byTeam[assignment.TeamId] = new DiagnosisSupportingTeamAssignmentDto
                {
                    TeamId = assignment.TeamId,
                    TechnicianId = assignment.TechnicianId,
                    AssistantTechnicianId = assignment.AssistantTechnicianId,
                    RequestedTechnicianCount = assignment.RequestedTechnicianCount
                };
            }

            return byTeam.Values
                .OrderBy(x => x.TeamId)
                .ToList();
        }

        private static List<string> ExtractSymptomsFromNotes(string? customerNotes)
        {
            var fallback = new List<string> { "Khong co ghi chu gi" };
            if (string.IsNullOrWhiteSpace(customerNotes))
            {
                return fallback;
            }

            var notes = customerNotes.Trim();
            try
            {
                using var doc = JsonDocument.Parse(notes);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var values = root.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => (x.GetString() ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .ToList();
                    return values.Count > 0 ? values : fallback;
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("symptoms", out var symptomsEl) && symptomsEl.ValueKind == JsonValueKind.Array)
                    {
                        var values = symptomsEl.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => (x.GetString() ?? string.Empty).Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct()
                            .ToList();
                        return values.Count > 0 ? values : fallback;
                    }

                    if (root.TryGetProperty("note", out var noteEl) && noteEl.ValueKind == JsonValueKind.String)
                    {
                        var note = (noteEl.GetString() ?? string.Empty).Trim();
                        return string.IsNullOrWhiteSpace(note) ? fallback : new List<string> { note };
                    }
                }
            }
            catch
            {
                // Legacy non-JSON format.
            }

            return new List<string> { notes };
        }

        private async Task<ActionResult<DiagnosisResponseDto>> AssignRequestAsync(long serviceOrderId, DiagnosisAcceptRequestDto request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thieu ky thuat vien nhan yeu cau." });
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return BadRequest(new { message = "Tho phu khong duoc trung voi tho chinh." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Yeu cau chan doan chua duoc tao." });
            }

            if (activeDiagnosis.Any(x => x.Status == DiagnosisStatusCompleted))
            {
                return BadRequest(new { message = "Chan doan da hoan tat, khong the nhan lai." });
            }

            if (!request.IsManagerOverride && activeDiagnosis.Any(x => x.Status == DiagnosisStatusAssigned && x.TechnicianId != request.TechnicianId))
            {
                return Conflict(new { message = "Yeu cau da duoc ky thuat vien khac nhan." });
            }

            var technicianExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!technicianExists)
            {
                return BadRequest(new { message = "Ky thuat vien khong ton tai." });
            }

            var resolvedTeamId = request.TeamId ?? await _diagnosisService.ResolveTeamIdForTechnicianAsync(request.TechnicianId);

            if (request.TeamId.HasValue)
            {
                var inTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId.Value);
                if (!inTeam)
                {
                    return BadRequest(new { message = "Ky thuat vien khong thuoc to duoc chon." });
                }
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return BadRequest(new { message = "Tho phu khong ton tai." });
                }

                if (resolvedTeamId.HasValue)
                {
                    var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, resolvedTeamId.Value);
                    if (!assistantInTeam)
                    {
                        return BadRequest(new { message = "Tho phu khong thuoc to duoc giao." });
                    }
                }
            }

            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                request.SupportingTeamIds,
                request.SupportingAssignments,
                resolvedTeamId);
            var normalizedSupportingTeamIds = normalizedSupportingAssignments
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            if (normalizedSupportingTeamIds.Count > 0 && !resolvedTeamId.HasValue)
            {
                return BadRequest(new { message = "Khi co to phoi hop, bat buoc chon to chinh." });
            }
            var requiresServiceOrderContext = request.BayId.HasValue || normalizedSupportingTeamIds.Count > 0;
            ServiceOrder? serviceOrder = null;
            if (requiresServiceOrderContext)
            {
                serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return NotFound(new { message = "Khong tim thay lenh sua chua." });
                }
            }

            if (normalizedSupportingTeamIds.Count > 0)
            {
                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.TechnicianId <= 0)
                    {
                        return BadRequest(new { message = "Moi to phoi hop phai chi dinh tho chinh." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue && supportingAssignment.AssistantTechnicianId.Value == supportingAssignment.TechnicianId)
                    {
                        return BadRequest(new { message = "Tho phu cua to phoi hop khong duoc trung tho chinh." });
                    }

                    var supportingTeamId = supportingAssignment.TeamId;
                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingTeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return BadRequest(new { message = $"To phoi hop #{supportingTeamId} khong ton tai." });
                    }

                    if (serviceOrder != null && supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return BadRequest(new { message = "To phoi hop phai thuoc cung chi nhanh voi lenh sua chua." });
                    }

                    var supportingLeadExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.TechnicianId);
                    if (!supportingLeadExists)
                    {
                        return BadRequest(new { message = $"Tho chinh cua to phoi hop #{supportingTeamId} khong ton tai." });
                    }

                    var supportingLeadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.TechnicianId, supportingTeamId);
                    if (!supportingLeadInTeam)
                    {
                        return BadRequest(new { message = $"Tho chinh cua to phoi hop #{supportingTeamId} khong thuoc to duoc chon." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        var supportingAssistantExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.AssistantTechnicianId.Value);
                        if (!supportingAssistantExists)
                        {
                            return BadRequest(new { message = $"Tho phu cua to phoi hop #{supportingTeamId} khong ton tai." });
                        }

                        var supportingAssistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.AssistantTechnicianId.Value, supportingTeamId);
                        if (!supportingAssistantInTeam)
                        {
                            return BadRequest(new { message = $"Tho phu cua to phoi hop #{supportingTeamId} khong thuoc to duoc chon." });
                        }
                    }
                }
            }

            if (request.BayId.HasValue)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder!.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return BadRequest(new { message = "Cau nang khong thuoc chi nhanh xu ly." });
                }
            }

            if (!request.IsManagerOverride)
            {
                var dispatchTag = activeDiagnosis
                    .Select(x => x.DispatchTag)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var dispatchTeamIds = new List<long>();
                if (resolvedTeamId.HasValue && resolvedTeamId.Value > 0)
                {
                    dispatchTeamIds.Add(resolvedTeamId.Value);
                }
                dispatchTeamIds.AddRange(normalizedSupportingTeamIds);

                foreach (var dispatchTeamId in dispatchTeamIds.Distinct())
                {
                    var isCompatible = await _diagnosisService.IsDispatchTagCompatibleAsync(dispatchTag, dispatchTeamId);
                    if (!isCompatible)
                    {
                        return Conflict(new { message = "Co to duoc chon khong dung chuyen mon cua lenh chan doan." });
                    }
                }
            }

            await _diagnosisService.AssignDiagnosesAsync(
                activeDiagnosis,
                request.TechnicianId,
                request.AssistantTechnicianId,
                resolvedTeamId,
                normalizedSupportingTeamIds,
                normalizedSupportingAssignments,
                request.BayId,
                request.IsManagerOverride);

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            return Ok(_mapper.Map<DiagnosisResponseDto>(response));
        }

        private async Task<IActionResult> DeleteRequestAsync(long serviceOrderId)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Khong tim thay lenh sua chua." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return Ok(new { message = "Khong co yeu cau chan doan de thu hoi." });
            }

            await _diagnosisService.RecallDiagnosesAsync(serviceOrder, activeDiagnosis);
            return Ok(new { message = "Da thu hoi yeu cau chan doan." });
        }

        private async Task<ActionResult<DiagnosisResponseDto>> CompleteAsync(long id, DiagnosisUpdateDto itemDto)
        {
            var existingItem = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (existingItem == null)
            {
                return NotFound(new { message = "Khong tim thay phien chan doan." });
            }

            _mapper.Map(itemDto, existingItem);
            await _diagnosisService.CompleteDiagnosisAsync(existingItem);

            var order = await _diagnosisService.GetServiceOrderAsync(existingItem.ServiceOrderId);
            if (order != null)
            {
                await _diagnosisService.UpdateServiceOrderStatusAsync(order, ServiceOrderStatusDiagnosing);
            }

            var dto = _mapper.Map<DiagnosisResponseDto>(existingItem);
            return Ok(dto);
        }
    }
}
