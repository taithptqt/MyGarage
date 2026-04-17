using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
        private const string DiagnosisStatusCanceled = nameof(DiagnosisStatus.CANCELED);
        private const string ServiceOrderStatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
        private static readonly IReadOnlyDictionary<string, string[]> DispatchTagKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENGINE_CHASSIS"] = new[]
            {
                "dong co",
                "gam",
                "phanh",
                "hop so",
                "ly hop",
                "con",
                "lai",
                "treo",
                "truyen dong",
                "ro ri dau",
                "nong may",
                "chet may"
            },
            ["ELECTRICAL_AC"] = new[]
            {
                "dien",
                "ac quy",
                "de may",
                "de no",
                "cam bien",
                "dieu hoa",
                "may lanh",
                "quat gio",
                "den",
                "kinh dien",
                "gat mua",
                "am thanh",
                "man hinh"
            },
            ["BODY_PAINT"] = new[]
            {
                "dong son",
                "than vo",
                "vo xe",
                "tray",
                "xuoc",
                "mop",
                "kinh",
                "noi that",
                "ngoai that",
                "danh bong",
                "cham soc",
                "rua xe",
                "son"
            },
            ["CAR_CARE"] = new[]
            {
                "cham soc",
                "rua xe",
                "detail",
                "detailing",
                "noi that",
                "ngoai that",
                "ceramic",
                "ppf"
            },
            ["MAINTENANCE"] = new[]
            {
                "bao duong",
                "dinh ky",
                "thay dau",
                "loc dau",
                "loc gio",
                "mini check",
                "quick check",
                "kiem tra tong quat",
                "tong quat",
                "checklist"
            }
        };

        public sealed record DiagnosisStatusPatchDto
        {
            public string? Status { get; init; }
            public string? Symptom { get; init; }
            public string? RootCause { get; init; }
            public string? Severity { get; init; }
        }

        private readonly IDiagnosisService _diagnosisService;
        private readonly IMapper _mapper;
        private readonly IQuotationWorkflowService _quotationWorkflowService;

        public DiagnosisController(IDiagnosisService diagnosisService, IMapper mapper, IQuotationWorkflowService quotationWorkflowService)
        {
            _diagnosisService = diagnosisService;
            _mapper = mapper;
            _quotationWorkflowService = quotationWorkflowService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll(
            [FromQuery] long? serviceOrderId,
            [FromQuery] bool requestOnly = false,
            [FromQuery] long? teamId = null)
        {
            var items = await _diagnosisService.GetAllFilteredAsync(serviceOrderId, requestOnly, teamId);
            var mappedDtos = _mapper.Map<IEnumerable<DiagnosisResponseDto>>(items).ToList();
            for (var i = 0; i < mappedDtos.Count && i < items.Count; i++)
            {
                ApplyAssignmentStateToDto(items[i], mappedDtos[i]);
            }
            var dtos = await EnrichDiagnosisDtosAsync(mappedDtos);
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
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
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
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var suggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
                serviceOrderId,
                NormalizeDispatchTag(dispatchTag),
                bufferMinutes,
                intakePackageCode);

            return Ok(suggestion);
        }

        [HttpGet("intake-packages")]
        public async Task<ActionResult<IEnumerable<DiagnosisIntakePackageOptionDto>>> GetIntakePackages()
        {
            var packages = await _diagnosisService.GetIntakePackagesAsync();
            return Ok(packages);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/handover-snapshots")]
        public async Task<ActionResult<DiagnosisHandoverSnapshotResponseDto>> CreateHandoverSnapshot(
            long serviceOrderId,
            [FromBody] DiagnosisHandoverSnapshotCreateDto? request)
        {
            if (request == null)
            {
            return BadRequest(new { message = "Dữ liệu T2 không hợp lệ." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
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
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var snapshot = await _diagnosisService.GetLatestHandoverSnapshotAsync(serviceOrderId);
            if (snapshot == null)
            {
                return NotFound(new { message = "Chưa có snapshot T2 sau chẩn đoán." });
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
            return BadRequest(new { message = "Thiếu thông tin duyệt/Từ chối của khách." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
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
            var item = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<DiagnosisResponseDto>(item);
            ApplyAssignmentStateToDto(item, dto);
            dto = await EnrichDiagnosisDtoAsync(dto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DiagnosisResponseDto>> Create([FromBody] DiagnosisCreateDto itemDto)
        {
            if (itemDto == null)
            {
            return BadRequest(new { message = "Dữ liệu chẩn đoán không hợp lệ." });
            }

            itemDto.Status = NormalizeStatus(itemDto.Status);
            itemDto.DispatchTag = NormalizeDispatchTag(itemDto.DispatchTag);

            var entity = _mapper.Map<Diagnosis>(itemDto);
            var createdItem = await _diagnosisService.AddAsync(entity);
            var responseDto = _mapper.Map<DiagnosisResponseDto>(createdItem);
            ApplyAssignmentStateToDto(createdItem, responseDto);
            responseDto = await EnrichDiagnosisDtoAsync(responseDto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> Update(long id, [FromBody] DiagnosisUpdateDto itemDto)
        {
            var existingItem = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var status = NormalizeStatus(itemDto.Status ?? existingItem.Status ?? DiagnosisStatusPending);

            _mapper.Map(itemDto, existingItem);
            existingItem.Status = status;
            existingItem.DispatchTag = NormalizeDispatchTag(existingItem.DispatchTag);
            existingItem.UpdatedAt = VietnamTime.Now;

            var updatedItem = await _diagnosisService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<DiagnosisResponseDto>(updatedItem);
            ApplyAssignmentStateToDto(updatedItem, responseDto);
            responseDto = await EnrichDiagnosisDtoAsync(responseDto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(responseDto);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<ActionResult<DiagnosisResponseDto>> RequestDiagnosis(long serviceOrderId, [FromBody] DiagnosisQueueCreateRequestDto? request)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var hasSignedCheckIn = await _diagnosisService.HasSignedCheckInAsync(serviceOrderId);
            if (!hasSignedCheckIn)
            {
            return BadRequest(new { message = "Chỉ được yêu cầu chẩn đoán sau khi khách đã ký check-in." });
            }

            var allDiagnosis = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            if (allDiagnosis.Any(x => x.Status == DiagnosisStatusCompleted))
            {
            return BadRequest(new { message = "Lệnh này đã có kết quả chẩn đoán hoàn tất." });
            }

            var activeDiagnosis = allDiagnosis
                .Where(x =>
                    !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (activeDiagnosis.Count > 0)
            {
                await _diagnosisService.ArchiveDiagnosesAsSupersededAsync(activeDiagnosis);
            }

            var dispatchTag = NormalizeDispatchTag(request?.DispatchTag);
            var symptoms = FilterSymptomsByDispatchTag(
                ExtractSymptomsFromNotes(serviceOrder.CustomerNotes),
                dispatchTag);
            var intakeSuggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
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
                    t1EstimatedHandover = intakeSuggestion.T1EstimatedHandover,
                    requiresDispatchReview = true
                });
            }

            if (intakeSuggestion.RequiresCustomerConsent && request?.CustomerConsentAccepted != true)
            {
                return BadRequest(new
                {
                    message = "Gói chẩn đoán 30p/60p cần xác nhận đồng ý của khách trước khi tạo yêu cầu."
                });
            }

            if (!intakeSuggestion.SuggestedTeamId.HasValue || intakeSuggestion.SuggestedTeamId.Value <= 0)
            {
                return Conflict(new
                {
                    message = "Không thể tự động xác định tổ đúng chuyên môn. Không tạo yêu cầu chẩn đoán.",
                    requiresDispatchReview = true,
                    dispatchTag = intakeSuggestion.DispatchTag,
                    conflictLevel = intakeSuggestion.ConflictLevel,
                    conflictMinutes = intakeSuggestion.ConflictMinutes
                });
            }

            var resolvedTeamId = intakeSuggestion.SuggestedTeamId!.Value;
            var resolvedDiagnoseBayId = await _diagnosisService.ResolveDiagnoseBayIdForBranchAsync(
                serviceOrder.ExecuteBranchId,
                intakeSuggestion.SuggestedBayId);
            var scheduledSlot = await _diagnosisService.ScheduleDiagnosisQueueSlotAsync(
                serviceOrderId,
                intakeSuggestion.PackageEffectiveMinutes,
                bufferMinutes: 15,
                preferredBayId: resolvedDiagnoseBayId);
            var finalAssignedBayId = scheduledSlot?.BayId ?? resolvedDiagnoseBayId;
            var newDiagnosis = await _diagnosisService.CreatePendingDiagnosesAsync(
                serviceOrderId,
                technicianId: null,
                symptoms,
                dispatchTag,
                assignedTeamId: resolvedTeamId,
                assignedBayId: finalAssignedBayId,
                assignmentNote: request?.AssignmentNote);

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

            var dto = _mapper.Map<DiagnosisResponseDto>(newDiagnosis.First());
            dto = await EnrichDiagnosisDtoAsync(dto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
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
                return BadRequest(new { message = "Thiếu thông tin kỹ thuật viên bắt đầu chẩn đoán." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
            return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
            return BadRequest(new { message = "Không có lệnh chẩn đoán đang hoạt động." });
            }

            var isAssignedMember = activeDiagnosis.Any(x =>
                _diagnosisService.ReadTechnicianId(x) == request.TechnicianId ||
                _diagnosisService.ReadAssistantTechnicianId(x).GetValueOrDefault() == request.TechnicianId);
            if (!isAssignedMember)
            {
                return Forbid();
            }

            if (request.BayId.HasValue && serviceOrder.ExecuteBranchId > 0)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder.ExecuteBranchId);
                if (!bayInBranch)
                {
            return BadRequest(new { message = "Cầu nâng được chọn không thuộc chi nhánh đang xử lý." });
                }

                var assignedTeamIds = activeDiagnosis
                    .Select(x => _diagnosisService.ReadAssignedTeamId(x))
                    .Where(x => x.HasValue && x.Value > 0)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();
                var hasPreApprovedBayAssignment = activeDiagnosis.Any(x => _diagnosisService.ReadAssignedBayId(x).GetValueOrDefault() == request.BayId.Value);

                if (!hasPreApprovedBayAssignment)
                {
                    foreach (var assignedTeamId in assignedTeamIds)
                    {
                        var (isCompatible, reason) = await _diagnosisService.ValidateTeamBayCompatibilityAsync(
                            serviceOrderId,
                            assignedTeamId,
                            request.BayId.Value);
                        if (!isCompatible)
                        {
                            return Conflict(new { message = reason });
                        }
                    }
                }
            }

            await _diagnosisService.StartDiagnosesAsync(activeDiagnosis, request.TechnicianId, request.BayId);
            await _diagnosisService.UpdateServiceOrderStatusAsync(serviceOrder, ServiceOrderStatusDiagnosing);

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            var responseDto = _mapper.Map<DiagnosisResponseDto>(response);
            ApplyAssignmentStateToDto(response, responseDto);
            responseDto = await EnrichDiagnosisDtoAsync(responseDto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(responseDto);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-approval")]
        public async Task<IActionResult> ApproveTechnical(long serviceOrderId, [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
            return BadRequest(new { message = "Thiếu thông tin tổ trưởng duyệt kỹ thuật." });
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
            return BadRequest(new { message = "Tổ trưởng không tồn tại." });
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
            return BadRequest(new { message = "Không có kết quả chẩn đoán để duyệt." });
            }
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            var executeBranchId = serviceOrder?.ExecuteBranchId;

            if (diagnoses.Any(x => !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase)))
            {
            return BadRequest(new { message = "Chỉ được duyệt kỹ thuật sau khi toàn bộ triệu chứng đã ở trạng thái COMPLETED." });
            }

            var isEscalatedToForeman = diagnoses.Any(x => _diagnosisService.IsTeamLeaderApprovalEscalated(x, executeBranchId));
            if (isEscalatedToForeman)
            {
            return BadRequest(new { message = "Phiếu đã quá SLA duyệt tổ trưởng và đã chuyển lên quản đốc xử lý." });
            }

            var finalAlreadyDecided = diagnoses.Any(x =>
            {
                var status = _diagnosisService.ReadFinalApprovalStatus(x);
                return string.Equals(status, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);
            });
            if (finalAlreadyDecided)
            {
            return BadRequest(new { message = "Phiếu đã có quyết định duyệt cuối, không thể duyệt kỹ thuật lại." });
            }

            var technicalAlreadyApproved = diagnoses.All(x =>
                string.Equals(
                    _diagnosisService.ReadTeamLeaderApprovalStatus(x),
                    nameof(ApprovalDecisionStatusCode.APPROVED),
                    StringComparison.OrdinalIgnoreCase));
            if (technicalAlreadyApproved)
            {
            return Ok(new { message = "Tổ trưởng đã duyệt kỹ thuật trước đó." });
            }

            await _diagnosisService.ApproveTechnicalAsync(diagnoses, request.TeamLeaderId, request.Note);
            return Ok(new { message = "Đã duyệt báo cáo kỹ thuật." });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/technical-rejection")]
        public async Task<IActionResult> RejectTechnical(long serviceOrderId, [FromBody] DiagnosisTechnicalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
                return BadRequest(new { message = "Thiếu thông tin tổ trưởng từ chối." });
            }

            if (string.IsNullOrWhiteSpace(request.Note))
            {
                return BadRequest(new { message = "Bắt buộc nhập lý do từ chối để yêu cầu thợ làm lại." });
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return BadRequest(new { message = "Tổ trưởng không tồn tại." });
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return BadRequest(new { message = "Không có kết quả chẩn đoán để xử lý." });
            }

            await _diagnosisService.RejectTechnicalAsync(diagnoses, request.TeamLeaderId, request.Note);
            return Ok(new { message = "Tổ trưởng đã từ chối và trả về cho thợ làm lại." });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/final-approval")]
        public async Task<IActionResult> ApproveFinal(long serviceOrderId, [FromBody] DiagnosisFinalApprovalRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
            return BadRequest(new { message = "Thiếu thông tin tổ trưởng duyệt báo giá." });
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
            return BadRequest(new { message = "Tổ trưởng không tồn tại." });
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
            return BadRequest(new { message = "Không có dữ liệu chẩn đoán để duyệt." });
            }

            if (diagnoses.Any(x => !_diagnosisService.ReadApprovedByForemanId(x).HasValue))
            {
            return BadRequest(new { message = "Cần tổ trưởng duyệt báo cáo kỹ thuật trước khi duyệt báo giá." });
            }

            await _diagnosisService.ApproveFinalAsync(diagnoses, request.TeamLeaderId, request.Note);
            var quotation = await _quotationWorkflowService.GenerateInitialQuotationFromDiagnosisAsync(
                serviceOrderId,
                request.TeamLeaderId,
                request.Note,
                diagnoses.Select(x => x.Id).Distinct().ToList());
            return Ok(new
            {
                    message = "Đã duyệt chốt kỹ thuật và sinh báo giá V1.",
                quotation
            });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/foreman-decision")]
        public async Task<IActionResult> ForemanDecision(long serviceOrderId, [FromBody] DiagnosisForemanDecisionRequestDto request)
        {
            if (request == null || request.ForemanId <= 0)
            {
            return BadRequest(new { message = "Thiếu thông tin quản đốc duyệt." });
            }

            var foreman = await _diagnosisService.GetStaffByIdAsync(request.ForemanId);
            if (foreman == null)
            {
            return BadRequest(new { message = "Quản đốc không tồn tại." });
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
            return BadRequest(new { message = "Không có dữ liệu chẩn đoán để xử lý." });
            }
            if (diagnoses.Any(x => !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase)))
            {
            return BadRequest(new { message = "Chỉ được duyệt/từ chối sau khi thợ đã gửi kết quả chẩn đoán hoàn tất." });
            }

            var teamLeaderApproved = diagnoses.All(x =>
            string.Equals(
                    _diagnosisService.ReadTeamLeaderApprovalStatus(x),
                    nameof(ApprovalDecisionStatusCode.APPROVED),
                    StringComparison.OrdinalIgnoreCase));

            var finalAlreadyDecided = diagnoses.Any(x =>
            {
                var status = _diagnosisService.ReadFinalApprovalStatus(x);
                return string.Equals(status, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);
            });
            if (finalAlreadyDecided)
            {
            return BadRequest(new { message = "Phiếu đã có quyết định duyệt cuối." });
            }

            if (request.Approve)
            {
                var bypassTeamLeader = !teamLeaderApproved;
                var firstTechnicianId = diagnoses
                    .Select(_diagnosisService.ReadTechnicianId)
                    .Where(x => x.HasValue && x.Value > 0)
                    .Select(x => x!.Value)
                    .FirstOrDefault();
                var technician = firstTechnicianId > 0
                    ? await _diagnosisService.GetStaffByIdAsync(firstTechnicianId)
                    : null;
                var bypassAuditNote = bypassTeamLeader
                    ? $"Quản đốc {foreman.FullName} duyệt vượt cấp từ thợ {technician?.FullName ?? "N/A"}"
                    : null;

                await _diagnosisService.ApproveFinalAsync(diagnoses, request.ForemanId, request.Note, bypassTeamLeader, bypassAuditNote);
                var quotation = await _quotationWorkflowService.GenerateInitialQuotationFromDiagnosisAsync(
                    serviceOrderId,
                    request.ForemanId,
                    request.Note,
                    diagnoses.Select(x => x.Id).Distinct().ToList());
                return Ok(new
                {
                    message = bypassTeamLeader
                        ? "Quản đốc đã duyệt vượt cấp và chuyển báo giá."
                        : "Quản đốc đã duyệt và chuyển báo giá.",
                    quotation
                });
            }

            await _diagnosisService.RejectFinalAsync(diagnoses, request.ForemanId, request.Note);
            return Ok(new { message = "Quản đốc từ chối, yêu cầu chẩn đoán lại." });
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
                return BadRequest(new { message = "Thi\u1EBFu th\u00F4ng tin ng\u01B0\u1EDDi y\u00EAu c\u1EA7u h\u1ED7 tr\u1EE3." });
            }

            var requester = await _diagnosisService.GetStaffByIdAsync(request.RequestedByStaffId);
            if (requester == null)
            {
                return BadRequest(new { message = "Ng\u01B0\u1EDDi y\u00EAu c\u1EA7u h\u1ED7 tr\u1EE3 kh\u00F4ng t\u1ED3n t\u1EA1i." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Kh\u00F4ng c\u00F3 l\u1EC7nh chu\u1EA9n \u0111o\u00E1n \u0111ang ho\u1EA1t \u0111\u1ED9ng \u0111\u1EC3 y\u00EAu c\u1EA7u h\u1ED7 tr\u1EE3." });
            }

            long? primaryTeamId = activeDiagnosis
                .Select(x => _diagnosisService.ReadAssignedTeamId(x))
                .Where(x => x.HasValue && x.Value > 0)
                .FirstOrDefault();
            if (!primaryTeamId.HasValue || primaryTeamId.Value <= 0)
            {
                primaryTeamId = null;
            }

            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                teamIds: null,
                assignments: request.SupportingAssignments,
                primaryTeamId: primaryTeamId);

            if (normalizedSupportingAssignments.Count > 0 && !primaryTeamId.HasValue)
            {
                return BadRequest(new { message = "Ch\u1EC9 cho ph\u00E9p y\u00EAu c\u1EA7u t\u1ED5 ph\u1ED1i h\u1EE3p sau khi \u0111\u00E3 c\u00F3 t\u1ED5 ch\u00EDnh." });
            }

            if (normalizedSupportingAssignments.Count > 0)
            {
                var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return NotFound(new { message = "Kh\u00F4ng t\u00ECm th\u1EA5y l\u1EC7nh s\u1EEDa ch\u1EEFa." });
                }

                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.RequestedTechnicianCount.GetValueOrDefault() <= 0)
                    {
                        return BadRequest(new { message = "M\u1ED7i t\u1ED5 ph\u1ED1i h\u1EE3p ph\u1EA3i c\u00F3 s\u1ED1 l\u01B0\u1EE3ng th\u1EE3 y\u00EAu c\u1EA7u l\u1EDBn h\u01A1n 0." });
                    }

                    if (supportingAssignment.TechnicianId > 0 || supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        return BadRequest(new { message = "T\u1ED5 tr\u01B0\u1EDFng t\u1ED5 ch\u00EDnh kh\u00F4ng \u0111\u01B0\u1EE3c ch\u1ECDn th\u1EE3 cho t\u1ED5 ph\u1ED1i h\u1EE3p." });
                    }

                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingAssignment.TeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return BadRequest(new { message = $"T\u1ED5 ph\u1ED1i h\u1EE3p #{supportingAssignment.TeamId} kh\u00F4ng t\u1ED3n t\u1EA1i." });
                    }

                    if (supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return BadRequest(new { message = "T\u1ED5 ph\u1ED1i h\u1EE3p ph\u1EA3i thu\u1ED9c c\u00F9ng chi nh\u00E1nh v\u1EDBi l\u1EC7nh s\u1EEDa ch\u1EEFa." });
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
                message = "\u0110\u00E3 ghi nh\u1EADn y\u00EAu c\u1EA7u h\u1ED7 tr\u1EE3 k\u1EF9 thu\u1EADt.",
                serviceOrderId,
                requestedBy = request.RequestedByStaffId
            });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/cancel-requests")]
        public async Task<IActionResult> RequestCancelAssignment(long serviceOrderId, [FromBody] DiagnosisCancelRequestDto request)
        {
            if (request == null || request.RequestedByStaffId <= 0)
            {
                return BadRequest(new { message = "Thiếu thông tin người yêu cầu hủy." });
            }

            var reason = (request.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Bắt buộc nhập lý do hủy (Sai chuyên môn, Quá tải cầu, Thiếu thiết bị...)." });
            }

            var requester = await _diagnosisService.GetStaffByIdAsync(request.RequestedByStaffId);
            if (requester == null)
            {
                return BadRequest(new { message = "Người yêu cầu hủy không tồn tại." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Không có lệnh chẩn đoán đang hoạt động để hủy." });
            }

            try
            {
                await _diagnosisService.RequestCancellationAsync(activeDiagnosis, request.RequestedByStaffId, reason);
                return Ok(new { message = "Đã gửi lệnh hủy sang trạng thái chờ duyệt của quản đốc." });
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
                return BadRequest(new { message = "Thiếu thông tin quản đốc phê duyệt." });
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return BadRequest(new { message = "Quản đốc không tồn tại." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var allDiagnosis = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            var pendingCancellation = allDiagnosis
                .Where(x => _diagnosisService.ReadCancellationRequestedAt(x).HasValue && !_diagnosisService.ReadCancellationDecisionAt(x).HasValue)
                .ToList();

            if (pendingCancellation.Count == 0)
            {
                return BadRequest(new { message = "Không có yêu cầu hủy nào đang chờ duyệt." });
            }

            await _diagnosisService.ApplyCancellationDecisionAsync(serviceOrder, pendingCancellation, request.Approve, request.ManagerId, request.Note);

            return Ok(new
            {
                message = request.Approve
                    ? "Quản đốc đã đồng ý hủy, lệnh được đưa về hàng chờ phân công lại."
                    : "Quản đốc từ chối hủy lệnh, tổ tiếp tục xử lý xe.",
                approved = request.Approve
            });
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/manager-override")]
        public async Task<ActionResult<DiagnosisResponseDto>> ManagerOverrideAssignment(long serviceOrderId, [FromBody] DiagnosisManagerOverrideDto request)
        {
            if (request == null || request.ManagerId <= 0 || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu thông tin override của quản đốc." });
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return BadRequest(new { message = "Thợ phụ không được trùng với thợ chính." });
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return BadRequest(new { message = "Quản đốc không tồn tại." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Không có lệnh chẩn đoán để override." });
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var technicianExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!technicianExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            var resolvedTeamId = request.TeamId ?? await _diagnosisService.ResolveTeamIdForTechnicianAsync(request.TechnicianId);
            if (request.TeamId.HasValue)
            {
                var inTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId.Value);
                if (!inTeam)
                {
                    return BadRequest(new { message = "Kỹ thuật viên không thuộc tổ được chỉ định." });
                }
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return BadRequest(new { message = "Thợ phụ không tồn tại." });
                }

                if (resolvedTeamId.HasValue)
                {
                    var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, resolvedTeamId.Value);
                    if (!assistantInTeam)
                    {
                        return BadRequest(new { message = "Thợ phụ không thuộc tổ được giao." });
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
                return BadRequest(new { message = "Khi có tổ phối hợp, bắt buộc chỉ định tổ chính." });
            }
            if (normalizedSupportingTeamIds.Count > 0)
            {
                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.TechnicianId <= 0)
                    {
                        return BadRequest(new { message = "Mỗi tổ phối hợp phải chỉ định thợ chính." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue && supportingAssignment.AssistantTechnicianId.Value == supportingAssignment.TechnicianId)
                    {
                        return BadRequest(new { message = "Thợ phụ của tổ phối hợp không được trùng thợ chính." });
                    }

                    var supportingTeamId = supportingAssignment.TeamId;
                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingTeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return BadRequest(new { message = $"Tổ phối hợp #{supportingTeamId} không tồn tại." });
                    }

                    if (supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return BadRequest(new { message = "Tổ phối hợp phải thuộc cùng chi nhánh với lệnh sửa chữa." });
                    }

                    var supportingLeadExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.TechnicianId);
                    if (!supportingLeadExists)
                    {
                        return BadRequest(new { message = $"Thợ chính của tổ phối hợp #{supportingTeamId} không tồn tại." });
                    }

                    var supportingLeadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.TechnicianId, supportingTeamId);
                    if (!supportingLeadInTeam)
                    {
                        return BadRequest(new { message = $"Thợ chính của tổ phối hợp #{supportingTeamId} không thuộc tổ được chọn." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        var supportingAssistantExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.AssistantTechnicianId.Value);
                        if (!supportingAssistantExists)
                        {
                            return BadRequest(new { message = $"Thợ phụ của tổ phối hợp #{supportingTeamId} không tồn tại." });
                        }

                        var supportingAssistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.AssistantTechnicianId.Value, supportingTeamId);
                        if (!supportingAssistantInTeam)
                        {
                            return BadRequest(new { message = $"Thợ phụ của tổ phối hợp #{supportingTeamId} không thuộc tổ được chọn." });
                        }
                    }
                }
            }

            if (request.BayId.HasValue)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return BadRequest(new { message = "Cầu nâng không thuộc chi nhánh xử lý." });
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
            ApplyAssignmentStateToDto(response, dto);
            dto = await EnrichDiagnosisDtoAsync(dto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(dto);
        }

        [HttpPost("service-orders/{serviceOrderId:long}/diagnosis-queues/supporting-team-selections")]
        public async Task<ActionResult<DiagnosisResponseDto>> SelectSupportingTeamAssignment(
            long serviceOrderId,
            [FromBody] DiagnosisSupportingTeamSelectionRequestDto request)
        {
            if (request == null || request.TeamLeaderId <= 0 || request.TeamId <= 0 || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu thông tin chọn thợ hỗ trợ của tổ phối hợp." });
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return BadRequest(new { message = "Thợ phụ không được trùng với thợ chính." });
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return BadRequest(new { message = "Tổ trưởng không tồn tại." });
            }

            var teamLeaderTeamId = teamLeader.TeamId;
            if (!teamLeaderTeamId.HasValue || teamLeaderTeamId.Value != request.TeamId)
            {
                return BadRequest(new { message = "Tổ trưởng chỉ được chọn nhân sự cho tổ của mình." });
            }

            var leadExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!leadExists)
            {
                return BadRequest(new { message = "Thợ chính không tồn tại." });
            }

            var leadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId);
            if (!leadInTeam)
            {
                return BadRequest(new { message = "Thợ chính không thuộc tổ được yêu cầu." });
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return BadRequest(new { message = "Thợ phụ không tồn tại." });
                }

                var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, request.TeamId);
                if (!assistantInTeam)
                {
                    return BadRequest(new { message = "Thợ phụ không thuộc tổ được yêu cầu." });
                }
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Không có lệnh chẩn đoán đang hoạt động để cập nhật nhân sự hỗ trợ." });
            }

            try
            {
                await _diagnosisService.AssignSupportingTeamSelectionAsync(
                    activeDiagnosis,
                    request.TeamId,
                    request.TeamLeaderId,
                    request.TechnicianId,
                    request.AssistantTechnicianId,
                    request.Note);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            var responseDto = _mapper.Map<DiagnosisResponseDto>(response);
            ApplyAssignmentStateToDto(response, responseDto);
            responseDto = await EnrichDiagnosisDtoAsync(responseDto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(responseDto);
        }

        [HttpPatch("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchStatus(long id, [FromBody] DiagnosisStatusPatchDto dto)
        {
            var status = NormalizeStatus(dto?.Status);
            if (status != DiagnosisStatusCompleted)
            {
                return BadRequest(new { message = "Chỉ hỗ trợ chuyển trạng thái sang COMPLETED." });
            }

            return await CompleteAsync(id, dto);
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
            var fallback = new List<string>();
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
                        .Where(IsMeaningfulSymptom)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
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
                            .Where(IsMeaningfulSymptom)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        return values.Count > 0 ? values : fallback;
                    }

                    if (root.TryGetProperty("note", out var noteEl) && noteEl.ValueKind == JsonValueKind.String)
                    {
                        var note = (noteEl.GetString() ?? string.Empty).Trim();
                        return IsMeaningfulSymptom(note) ? new List<string> { note } : fallback;
                    }
                }
            }
            catch
            {
                // Legacy non-JSON format.
            }

            return IsMeaningfulSymptom(notes)
                ? new List<string> { notes }
                : fallback;
        }

        private static List<string> FilterSymptomsByDispatchTag(IReadOnlyList<string>? symptoms, string? dispatchTag)
        {
            var normalizedSymptoms = (symptoms ?? Array.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedSymptoms.Count == 0)
            {
                return new List<string>();
            }

            var dispatchTags = (dispatchTag ?? string.Empty)
                .Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (dispatchTags.Count == 0)
            {
                return normalizedSymptoms;
            }

            var filtered = normalizedSymptoms
                .Where(symptom =>
                {
                    var normalizedSymptom = NormalizeKeywordText(symptom);
                    return dispatchTags.Any(tag => IsSymptomCompatibleWithDispatchTag(normalizedSymptom, tag));
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return filtered.Count > 0 ? filtered : normalizedSymptoms;
        }

        private static bool IsMeaningfulSymptom(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizeKeywordText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized == "khong co" ||
                normalized == "khong co ghi chu" ||
                normalized == "khong co ghi chu gi" ||
                normalized == "khong co mo ta")
            {
                return false;
            }

            if (normalized.StartsWith("[tiep nhan]", StringComparison.Ordinal) &&
                normalized.Contains("khong co", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalized.StartsWith("[flow:", StringComparison.Ordinal) ||
                normalized.StartsWith("[cluster:", StringComparison.Ordinal) ||
                normalized.StartsWith("[dispatch", StringComparison.Ordinal) ||
                normalized.StartsWith("[auto_dispatch", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static bool IsSymptomCompatibleWithDispatchTag(string normalizedSymptom, string dispatchTag)
        {
            if (!DispatchTagKeywords.TryGetValue(dispatchTag, out var keywords) || keywords.Length == 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(normalizedSymptom))
            {
                return false;
            }

            return keywords.Any(keyword => normalizedSymptom.Contains(keyword, StringComparison.Ordinal));
        }

        private static string NormalizeKeywordText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = raw.Trim().ToLowerInvariant();
            normalized = normalized.Replace('\u0111', 'd');

            var decomposed = normalized.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private async Task<List<DiagnosisResponseDto>> EnrichDiagnosisDtosAsync(IEnumerable<DiagnosisResponseDto> source)
        {
            var list = source?.ToList() ?? new List<DiagnosisResponseDto>();
            if (list.Count == 0)
            {
                return list;
            }

            var teamCache = new Dictionary<long, Team?>();
            var staffCache = new Dictionary<long, Staff?>();
            foreach (var dto in list)
            {
                await EnrichDiagnosisDtoAsync(dto, teamCache, staffCache);
            }

            return list;
        }

        private async Task<DiagnosisResponseDto> EnrichDiagnosisDtoAsync(
            DiagnosisResponseDto dto,
            IDictionary<long, Team?> teamCache,
            IDictionary<long, Staff?> staffCache)
        {
            long? resolvedTeamId = dto.AssignedTeamId;
            if ((!resolvedTeamId.HasValue || resolvedTeamId.Value <= 0) && dto.TechnicianId > 0)
            {
                resolvedTeamId = await _diagnosisService.ResolveTeamIdForTechnicianAsync(dto.TechnicianId!.Value);
            }

            if (resolvedTeamId.HasValue && resolvedTeamId.Value > 0)
            {
                dto.AssignedTeamId = resolvedTeamId.Value;

                if (!teamCache.TryGetValue(resolvedTeamId.Value, out var team))
                {
                    team = await _diagnosisService.GetTeamByIdAsync(resolvedTeamId.Value);
                    teamCache[resolvedTeamId.Value] = team;
                }

                if (team != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.AssignedTeamName))
                    {
                        dto.AssignedTeamName = team.Name;
                    }

                    if (string.IsNullOrWhiteSpace(dto.AssignedTeamSpecialtyTag))
                    {
                        dto.AssignedTeamSpecialtyTag = team.SpecialtyTag;
                    }

                    if ((!dto.AssignedTeamLeaderId.HasValue || dto.AssignedTeamLeaderId.Value <= 0) &&
                        team.LeaderId.HasValue && team.LeaderId.Value > 0)
                    {
                        dto.AssignedTeamLeaderId = team.LeaderId.Value;
                    }
                }
            }

            long? leaderId = dto.AssignedTeamLeaderId;
            if ((!leaderId.HasValue || leaderId.Value <= 0) && dto.TechnicianId > 0)
            {
                leaderId = dto.TechnicianId;
                dto.AssignedTeamLeaderId = dto.TechnicianId;
            }

            if (leaderId.HasValue && leaderId.Value > 0)
            {
                if (!staffCache.TryGetValue(leaderId.Value, out var leader))
                {
                    leader = await _diagnosisService.GetStaffByIdAsync(leaderId.Value);
                    staffCache[leaderId.Value] = leader;
                }

                if (leader != null && string.IsNullOrWhiteSpace(dto.AssignedTeamLeaderName))
                {
                    dto.AssignedTeamLeaderName = leader.FullName;
                }
            }

            if (dto.ApprovedByForemanId.HasValue && dto.ApprovedByForemanId.Value > 0)
            {
                if (!staffCache.TryGetValue(dto.ApprovedByForemanId.Value, out var technicalApprover))
                {
                    technicalApprover = await _diagnosisService.GetStaffByIdAsync(dto.ApprovedByForemanId.Value);
                    staffCache[dto.ApprovedByForemanId.Value] = technicalApprover;
                }

                if (technicalApprover != null && string.IsNullOrWhiteSpace(dto.ApprovedByForemanName))
                {
                    dto.ApprovedByForemanName = technicalApprover.FullName;
                }
            }

            long? finalApproverId = dto.ApprovedByTeamLeaderId;
            if ((!finalApproverId.HasValue || finalApproverId.Value <= 0) &&
                dto.ApprovedByTeamLeaderAt.HasValue &&
                dto.UpdatedBy.HasValue &&
                dto.UpdatedBy.Value > 0)
            {
                finalApproverId = dto.UpdatedBy.Value;
                dto.ApprovedByTeamLeaderId = finalApproverId;
            }

            if (finalApproverId.HasValue && finalApproverId.Value > 0)
            {
                if (!staffCache.TryGetValue(finalApproverId.Value, out var finalApprover))
                {
                    finalApprover = await _diagnosisService.GetStaffByIdAsync(finalApproverId.Value);
                    staffCache[finalApproverId.Value] = finalApprover;
                }

                if (finalApprover != null && string.IsNullOrWhiteSpace(dto.ApprovedByTeamLeaderName))
                {
                    dto.ApprovedByTeamLeaderName = finalApprover.FullName;
                }
            }

            return dto;
        }

        private void ApplyAssignmentStateToDto(Diagnosis source, DiagnosisResponseDto target)
        {
            target.DiagnosisAssignmentId = _diagnosisService.ReadDiagnosisAssignmentId(source);
            target.AssignedTeamId = _diagnosisService.ReadAssignedTeamId(source);
            target.TechnicianId = _diagnosisService.ReadTechnicianId(source);
            target.AssistantTechnicianId = _diagnosisService.ReadAssistantTechnicianId(source);
            target.AssignedBayId = _diagnosisService.ReadAssignedBayId(source);
            var teamLeaderStatus = _diagnosisService.ReadTeamLeaderApprovalStatus(source);
            var executeBranchId = source.ServiceOrder?.ExecuteBranchId;
            var isEscalated = _diagnosisService.IsTeamLeaderApprovalEscalated(source, executeBranchId);
            var isWarning = _diagnosisService.IsTeamLeaderApprovalWarning(source, executeBranchId);
            target.TeamLeaderApprovalPendingMinutes = _diagnosisService.ReadTeamLeaderApprovalPendingMinutes(source);
            target.IsTeamLeaderApprovalWarning = isWarning;
            target.IsTeamLeaderApprovalEscalated = isEscalated;
            target.TeamLeaderApprovalStatus = isEscalated &&
                                              string.Equals(teamLeaderStatus, nameof(ApprovalDecisionStatusCode.PENDING), StringComparison.OrdinalIgnoreCase)
                ? nameof(ApprovalDecisionStatusCode.ESCALATED)
                : teamLeaderStatus;
            target.IsEscalated = target.IsEscalated || isEscalated;
            target.FinalApprovalStatus = _diagnosisService.ReadFinalApprovalStatus(source);
            target.ForemanRejectReason = _diagnosisService.ReadForemanRejectReason(source);
            target.ApprovedByForemanId = _diagnosisService.ReadApprovedByForemanId(source);
            target.ApprovedByForemanAt = _diagnosisService.ReadApprovedByForemanAt(source);
            target.ApprovedByTeamLeaderId = _diagnosisService.ReadApprovedByTeamLeaderId(source);
            target.ApprovedByTeamLeaderAt = _diagnosisService.ReadApprovedByTeamLeaderAt(source);
            target.TeamLeaderNote = _diagnosisService.ReadTeamLeaderNote(source);
            target.ForemanNote = _diagnosisService.ReadForemanNote(source);
            target.CancellationRequestedAt = _diagnosisService.ReadCancellationRequestedAt(source);
            target.CancellationDecisionAt = _diagnosisService.ReadCancellationDecisionAt(source);
        }

        private async Task<ActionResult<DiagnosisResponseDto>> AssignRequestAsync(long serviceOrderId, DiagnosisAcceptRequestDto request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu kỹ thuật viên nhận yêu cầu." });
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return BadRequest(new { message = "Thợ phụ không được trùng với thợ chính." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Yêu cầu chẩn đoán chưa được tạo." });
            }

            if (activeDiagnosis.Any(x => x.Status == DiagnosisStatusCompleted))
            {
                return BadRequest(new { message = "Chẩn đoán đã hoàn tất, không thể nhận lại." });
            }

            if (!request.IsManagerOverride && activeDiagnosis.Any(x => x.Status == DiagnosisStatusAssigned && _diagnosisService.ReadTechnicianId(x) != request.TechnicianId))
            {
                return Conflict(new { message = "Yêu cầu đã được kỹ thuật viên khác nhận." });
            }

            var technicianExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!technicianExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            var resolvedTeamId = request.TeamId ?? await _diagnosisService.ResolveTeamIdForTechnicianAsync(request.TechnicianId);

            if (request.TeamId.HasValue)
            {
                var inTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId.Value);
                if (!inTeam)
                {
                    return BadRequest(new { message = "Kỹ thuật viên không thuộc tổ được chọn." });
                }
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return BadRequest(new { message = "Thợ phụ không tồn tại." });
                }

                if (resolvedTeamId.HasValue)
                {
                    var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, resolvedTeamId.Value);
                    if (!assistantInTeam)
                    {
                        return BadRequest(new { message = "Thợ phụ không thuộc tổ được giao." });
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
                return BadRequest(new { message = "Khi có tổ phối hợp, bắt buộc chọn tổ chính." });
            }
            var requiresServiceOrderContext = request.BayId.HasValue || normalizedSupportingTeamIds.Count > 0;
            ServiceOrder? serviceOrder = null;
            if (requiresServiceOrderContext)
            {
                serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
                }
            }

            if (normalizedSupportingTeamIds.Count > 0)
            {
                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.TechnicianId <= 0)
                    {
                        return BadRequest(new { message = "Mỗi tổ phối hợp phải chỉ định thợ chính." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue && supportingAssignment.AssistantTechnicianId.Value == supportingAssignment.TechnicianId)
                    {
                        return BadRequest(new { message = "Thợ phụ của tổ phối hợp không được trùng thợ chính." });
                    }

                    var supportingTeamId = supportingAssignment.TeamId;
                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingTeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return BadRequest(new { message = $"Tổ phối hợp #{supportingTeamId} không tồn tại." });
                    }

                    if (serviceOrder != null && supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return BadRequest(new { message = "Tổ phối hợp phải thuộc cùng chi nhánh với lệnh sửa chữa." });
                    }

                    var supportingLeadExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.TechnicianId);
                    if (!supportingLeadExists)
                    {
                        return BadRequest(new { message = $"Thợ chính của tổ phối hợp #{supportingTeamId} không tồn tại." });
                    }

                    var supportingLeadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.TechnicianId, supportingTeamId);
                    if (!supportingLeadInTeam)
                    {
                        return BadRequest(new { message = $"Thợ chính của tổ phối hợp #{supportingTeamId} không thuộc tổ được chọn." });
                    }

                    if (supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        var supportingAssistantExists = await _diagnosisService.TechnicianExistsAsync(supportingAssignment.AssistantTechnicianId.Value);
                        if (!supportingAssistantExists)
                        {
                            return BadRequest(new { message = $"Thợ phụ của tổ phối hợp #{supportingTeamId} không tồn tại." });
                        }

                        var supportingAssistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.AssistantTechnicianId.Value, supportingTeamId);
                        if (!supportingAssistantInTeam)
                        {
                            return BadRequest(new { message = $"Thợ phụ của tổ phối hợp #{supportingTeamId} không thuộc tổ được chọn." });
                        }
                    }
                }
            }

            if (request.BayId.HasValue)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder!.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return BadRequest(new { message = "Cầu nâng không thuộc chi nhánh xử lý." });
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
                        return Conflict(new { message = "Có tổ được chọn không đúng chuyên môn của lệnh chẩn đoán." });
                    }
                }

                if (request.BayId.HasValue)
                {
                    foreach (var dispatchTeamId in dispatchTeamIds.Distinct())
                    {
                        var compatibility = await _diagnosisService.ValidateTeamBayCompatibilityAsync(
                            serviceOrder!.Id,
                            dispatchTeamId,
                            request.BayId.Value);
                        if (!compatibility.IsCompatible)
                        {
                            return Conflict(new { message = compatibility.Reason });
                        }
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
            var responseDto = _mapper.Map<DiagnosisResponseDto>(response);
            ApplyAssignmentStateToDto(response, responseDto);
            responseDto = await EnrichDiagnosisDtoAsync(responseDto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(responseDto);
        }

        private async Task<IActionResult> DeleteRequestAsync(long serviceOrderId)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var workflowDiagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (workflowDiagnoses.Count == 0)
            {
                return Ok(new { message = "Không có phiếu chẩn đoán để hủy." });
            }

            static bool IsFinalDecisionMade(string? finalStatus) =>
                string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);

            var cancellableDiagnoses = workflowDiagnoses
                .Where(x =>
                    !string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                    !IsFinalDecisionMade(_diagnosisService.ReadFinalApprovalStatus(x)))
                .ToList();

            if (cancellableDiagnoses.Count == 0)
            {
                return BadRequest(new { message = "Phiếu chẩn đoán đã được quản đốc quyết định, không thể hủy." });
            }

            await _diagnosisService.RecallDiagnosesAsync(serviceOrder, cancellableDiagnoses);
            return Ok(new { message = "Đã hủy phiếu chẩn đoán." });
        }

        private async Task<ActionResult<DiagnosisResponseDto>> CompleteAsync(long id, DiagnosisStatusPatchDto? itemDto)
        {
            var existingItem = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (existingItem == null)
            {
                return NotFound(new { message = "Không tìm thấy phiên chẩn đoán." });
            }

            if (itemDto != null)
            {
                if (!string.IsNullOrWhiteSpace(itemDto.Symptom))
                {
                    existingItem.Symptom = itemDto.Symptom;
                }

                if (!string.IsNullOrWhiteSpace(itemDto.RootCause))
                {
                    existingItem.RootCause = itemDto.RootCause;
                }

                if (!string.IsNullOrWhiteSpace(itemDto.Severity))
                {
                    existingItem.Severity = itemDto.Severity;
                }
            }

            existingItem.Status = DiagnosisStatusCompleted;
            await _diagnosisService.CompleteDiagnosisAsync(existingItem);

            var order = await _diagnosisService.GetServiceOrderAsync(existingItem.ServiceOrderId);
            if (order != null)
            {
                await _diagnosisService.UpdateServiceOrderStatusAsync(order, ServiceOrderStatusDiagnosing);
            }

            var dto = _mapper.Map<DiagnosisResponseDto>(existingItem);
            ApplyAssignmentStateToDto(existingItem, dto);
            dto = await EnrichDiagnosisDtoAsync(dto, new Dictionary<long, Team?>(), new Dictionary<long, Staff?>());
            return Ok(dto);
        }
    }
}



