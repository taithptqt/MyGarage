using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/diagnoses")]
    [ApiController]
    public class DiagnosisController : ControllerBase
    {
        private const string DiagnosisStatusPending = "PENDING";
        private const string DiagnosisStatusAssigned = "ASSIGNED";
        private const string DiagnosisStatusCompleted = "COMPLETED";
        private const string CancellationStatusPending = "PENDING";

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

            var status = NormalizeStatus(itemDto.Status);
            if (status == "DRAFT")
            {
                status = DiagnosisStatusPending;
            }

            itemDto.Status = status;
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
            if (status == "DRAFT")
            {
                return BadRequest(new { message = "Khong con ho tro trang thai DRAFT." });
            }

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
            var placeholderTechnicianId = await _diagnosisService.ResolvePlaceholderTechnicianIdAsync(serviceOrder, dispatchTag);
            var newDiagnosis = await _diagnosisService.CreatePendingDiagnosesAsync(serviceOrderId, placeholderTechnicianId, symptoms, dispatchTag);

            await _diagnosisService.UpdateServiceOrderStatusAsync(serviceOrder, "INSPECTING");

            var dto = _mapper.Map<DiagnosisResponseDto>(newDiagnosis.First());
            return Ok(dto);
        }

        [HttpPatch("service-orders/{serviceOrderId:long}/diagnosis-queues")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchRequestAssignment(long serviceOrderId, [FromBody] DiagnosisAcceptRequestDto request)
        {
            return await AssignRequestAsync(serviceOrderId, request);
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

            await _diagnosisService.MarkSupportRequestedAsync(activeDiagnosis, request.RequestedByStaffId, request.Note);
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
                .Where(x => string.Equals(x.CancellationStatus, CancellationStatusPending, StringComparison.OrdinalIgnoreCase))
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

            await _diagnosisService.AssignDiagnosesAsync(activeDiagnosis, request.TechnicianId, resolvedTeamId, managerOverride: true);

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();

            var dto = _mapper.Map<DiagnosisResponseDto>(response);
            return Ok(dto);
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
            return (rawStatus ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
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

            if (!request.IsManagerOverride)
            {
                var dispatchTag = activeDiagnosis
                    .Select(x => x.DispatchTag)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var isCompatible = await _diagnosisService.IsDispatchTagCompatibleAsync(dispatchTag, resolvedTeamId);
                if (!isCompatible)
                {
                    return Conflict(new { message = "To duoc chon khong dung chuyen mon cua lenh chan doan." });
                }
            }

            await _diagnosisService.AssignDiagnosesAsync(activeDiagnosis, request.TechnicianId, resolvedTeamId, request.IsManagerOverride);

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
                var allCompleted = await _diagnosisService.AreAllDiagnosesCompletedAsync(existingItem.ServiceOrderId);
                await _diagnosisService.UpdateServiceOrderStatusAsync(order, allCompleted ? "WAITING_APPROVAL" : "INSPECTING");
            }

            var dto = _mapper.Map<DiagnosisResponseDto>(existingItem);
            return Ok(dto);
        }
    }
}
