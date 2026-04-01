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
        public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll([FromQuery] long? serviceOrderId, [FromQuery] bool requestOnly = false)
        {
            var items = await _diagnosisService.GetAllFilteredAsync(serviceOrderId, requestOnly);
            var dtos = _mapper.Map<IEnumerable<DiagnosisResponseDto>>(items);
            return Ok(dtos);
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
                return BadRequest(new { message = "Dữ liệu chẩn đoán không hợp lệ." });
            }

            var status = (itemDto.Status ?? "PENDING").Trim().ToUpperInvariant();
            if (status == "DRAFT")
            {
                status = "PENDING";
            }

            itemDto.Status = status;
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

            var status = (itemDto.Status ?? existingItem.Status ?? "PENDING").Trim().ToUpperInvariant();
            if (status == "DRAFT")
            {
                return BadRequest(new { message = "Không còn hỗ trợ trạng thái DRAFT." });
            }

            _mapper.Map(itemDto, existingItem);
            existingItem.Status = status;
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
        public async Task<ActionResult<DiagnosisResponseDto>> RequestDiagnosis(long serviceOrderId)
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
            if (allDiagnosis.Any(x => x.Status == "COMPLETED"))
            {
                return BadRequest(new { message = "Lệnh này đã có kết quả chẩn đoán hoàn tất." });
            }

            var activeDiagnosis = allDiagnosis.Where(x => x.Status != "COMPLETED").ToList();
            if (activeDiagnosis.Count > 0)
            {
                var activeIds = activeDiagnosis.Select(x => x.Id).ToList();
                await _diagnosisService.RemoveRecommendationsByDiagnosisIdsAsync(activeIds);
                await _diagnosisService.RemoveDiagnosesAsync(activeDiagnosis);
            }

            var symptoms = ExtractSymptomsFromNotes(serviceOrder.CustomerNotes);
            var placeholderTechnicianId = await _diagnosisService.ResolvePlaceholderTechnicianIdAsync(serviceOrder);
            var newDiagnosis = await _diagnosisService.CreatePendingDiagnosesAsync(serviceOrderId, placeholderTechnicianId, symptoms);

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

        [HttpPatch("{id:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> PatchStatus(long id, [FromBody] DiagnosisStatusPatchDto dto)
        {
            var status = (dto?.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (status != "COMPLETED")
            {
                return BadRequest(new { message = "Chỉ hỗ trợ chuyển trạng thái sang COMPLETED." });
            }

            var payload = new DiagnosisUpdateDto
            {
                Status = "COMPLETED",
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

        private static List<string> ExtractSymptomsFromNotes(string? customerNotes)
        {
            var fallback = new List<string> { "Không có ghi chú gì" };
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
                        .Select(x => (x.GetString() ?? "").Trim())
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
                            .Select(x => (x.GetString() ?? "").Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct()
                            .ToList();
                        return values.Count > 0 ? values : fallback;
                    }

                    if (root.TryGetProperty("note", out var noteEl) && noteEl.ValueKind == JsonValueKind.String)
                    {
                        var note = (noteEl.GetString() ?? "").Trim();
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
                return BadRequest(new { message = "Thiếu kỹ thuật viên nhận yêu cầu." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return BadRequest(new { message = "Yêu cầu chẩn đoán chưa được tạo." });
            }

            if (activeDiagnosis.Any(x => x.Status == "COMPLETED"))
            {
                return BadRequest(new { message = "Chẩn đoán đã hoàn tất, không thể nhận lại." });
            }

            if (activeDiagnosis.Any(x => x.Status == "ASSIGNED" && x.TechnicianId != request.TechnicianId))
            {
                return Conflict(new { message = "Yêu cầu đã được kỹ thuật viên khác nhận." });
            }

            var technicianExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!technicianExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            await _diagnosisService.AssignDiagnosesAsync(activeDiagnosis, request.TechnicianId);

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
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return Ok(new { message = "Không có yêu cầu chẩn đoán để thu hồi." });
            }

            await _diagnosisService.RecallDiagnosesAsync(serviceOrder, activeDiagnosis);
            return Ok(new { message = "Đã thu hồi yêu cầu chẩn đoán." });
        }

        private async Task<ActionResult<DiagnosisResponseDto>> CompleteAsync(long id, DiagnosisUpdateDto itemDto)
        {
            var existingItem = await _diagnosisService.GetDiagnosisEntityAsync(id);
            if (existingItem == null)
            {
                return NotFound(new { message = "Không tìm thấy phiên chẩn đoán." });
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
