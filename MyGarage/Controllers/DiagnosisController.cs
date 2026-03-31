using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/diagnoses")]
[Route("api/[controller]")]
    [ApiController]
    public class DiagnosisController : ControllerBase
    {
        private readonly IDiagnosisService _diagnosisService;
        private readonly IMapper _mapper;
        private readonly GarageContext _context;

        public DiagnosisController(IDiagnosisService diagnosisService, IMapper mapper, GarageContext context)
        {
            _diagnosisService = diagnosisService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiagnosisResponseDto>>> GetAll([FromQuery] long? serviceOrderId, [FromQuery] bool requestOnly = false)
        {
            var query = _context.Diagnoses.AsNoTracking().AsQueryable();

            if (serviceOrderId.HasValue)
            {
                query = query.Where(x => x.ServiceOrderId == serviceOrderId.Value);
            }

            if (requestOnly)
            {
                query = query.Where(x => x.Status == "PENDING" || x.Status == "ASSIGNED");
            }

            var items = await query
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();

            var dtos = _mapper.Map<IEnumerable<DiagnosisResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
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

        [HttpPut("{id}")]
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

        [HttpPost("requests/{serviceOrderId:long}")]
        public async Task<ActionResult<DiagnosisResponseDto>> RequestDiagnosis(long serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var hasSignedCheckIn = await HasSignedCheckInAsync(serviceOrderId);
            if (!hasSignedCheckIn)
            {
                return BadRequest(new { message = "Chỉ được yêu cầu chuẩn đoán sau khi khách đã ký check-in." });
            }

            var allDiagnosis = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .ToListAsync();

            if (allDiagnosis.Any(x => x.Status == "COMPLETED"))
            {
                return BadRequest(new { message = "Lệnh này đã có kết quả chẩn đoán hoàn tất." });
            }

            var activeDiagnosis = allDiagnosis.Where(x => x.Status != "COMPLETED").ToList();
            if (activeDiagnosis.Count > 0)
            {
                var activeIds = activeDiagnosis.Select(x => x.Id).ToList();
                var recs = await _context.DiagnoseRecommendations
                    .Where(x => activeIds.Contains(x.DiagnoseId))
                    .ToListAsync();
                if (recs.Count > 0)
                {
                    _context.DiagnoseRecommendations.RemoveRange(recs);
                }
                _context.Diagnoses.RemoveRange(activeDiagnosis);
            }

            var symptoms = ExtractSymptomsFromNotes(serviceOrder.CustomerNotes);
            var placeholderTechnicianId = await ResolvePlaceholderTechnicianId(serviceOrder);
            var now = DateTime.UtcNow;
            var newDiagnosis = symptoms.Select(symptom => new Diagnosis
            {
                ServiceOrderId = serviceOrderId,
                TechnicianId = placeholderTechnicianId,
                Symptom = symptom,
                Status = "PENDING",
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            _context.Diagnoses.AddRange(newDiagnosis);

            serviceOrder.Status = "INSPECTING";
            serviceOrder.UpdatedAt = DateTime.UtcNow;
            _context.ServiceOrders.Update(serviceOrder);

            await _context.SaveChangesAsync();

            var dto = _mapper.Map<DiagnosisResponseDto>(newDiagnosis.First());
            return Ok(dto);
        }

        [HttpPost("requests/{serviceOrderId:long}/accept")]
        public async Task<ActionResult<DiagnosisResponseDto>> AcceptDiagnosisRequest(long serviceOrderId, [FromBody] DiagnosisAcceptRequestDto request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu kỹ thuật viên nhận yêu cầu." });
            }

            var activeDiagnosis = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.Status == "PENDING" || x.Status == "ASSIGNED")
                .ToListAsync();

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

            var technicianExists = await _context.Staffs.AnyAsync(x => x.Id == request.TechnicianId);
            if (!technicianExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            foreach (var diagnosis in activeDiagnosis)
            {
                diagnosis.TechnicianId = request.TechnicianId;
                diagnosis.Status = "ASSIGNED";
                diagnosis.UpdatedAt = DateTime.UtcNow;
            }
            _context.Diagnoses.UpdateRange(activeDiagnosis);
            await _context.SaveChangesAsync();

            var response = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            return Ok(_mapper.Map<DiagnosisResponseDto>(response));
        }

        [HttpPost("requests/{serviceOrderId:long}/recall")]
        public async Task<IActionResult> RecallDiagnosisRequest(long serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var activeDiagnosis = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.Status == "PENDING" || x.Status == "ASSIGNED")
                .ToListAsync();

            if (activeDiagnosis.Count == 0)
            {
                return Ok(new { message = "Không có yêu cầu chẩn đoán để thu hồi." });
            }

            var activeIds = activeDiagnosis.Select(x => x.Id).ToList();
            var recs = await _context.DiagnoseRecommendations
                .Where(x => activeIds.Contains(x.DiagnoseId))
                .ToListAsync();
            if (recs.Count > 0)
            {
                _context.DiagnoseRecommendations.RemoveRange(recs);
            }

            _context.Diagnoses.RemoveRange(activeDiagnosis);

            if (serviceOrder.Status == "INSPECTING")
            {
                serviceOrder.Status = "NEW";
            }
            serviceOrder.UpdatedAt = DateTime.UtcNow;
            _context.ServiceOrders.Update(serviceOrder);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã thu hồi yêu cầu chẩn đoán." });
        }

        [HttpPost("{id:long}/complete")]
        public async Task<ActionResult<DiagnosisResponseDto>> Complete(long id, [FromBody] DiagnosisUpdateDto itemDto)
        {
            var existingItem = await _context.Diagnoses.FirstOrDefaultAsync(x => x.Id == id);
            if (existingItem == null)
            {
                return NotFound(new { message = "Không tìm thấy phiên chẩn đoán." });
            }

            _mapper.Map(itemDto, existingItem);
            existingItem.Status = "COMPLETED";
            existingItem.DiagnosedAt = DateTime.UtcNow;
            existingItem.UpdatedAt = DateTime.UtcNow;

            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == existingItem.ServiceOrderId);
            if (order != null)
            {
                var allStatus = await _context.Diagnoses
                    .Where(x => x.ServiceOrderId == existingItem.ServiceOrderId)
                    .Select(x => x.Status)
                    .ToListAsync();
                var allCompleted = allStatus.Count > 0 && allStatus.All(s => s == "COMPLETED");
                order.Status = allCompleted ? "WAITING_APPROVAL" : "INSPECTING";
                order.UpdatedAt = DateTime.UtcNow;
                _context.ServiceOrders.Update(order);
            }

            await _context.SaveChangesAsync();
            var dto = _mapper.Map<DiagnosisResponseDto>(existingItem);
            return Ok(dto);
        }

        [HttpDelete("{id}")]
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

        private async Task<long> ResolvePlaceholderTechnicianId(ServiceOrder serviceOrder)
        {
            if (serviceOrder.ServiceAdvisorId.HasValue)
            {
                var advisorExists = await _context.Staffs.AnyAsync(x => x.Id == serviceOrder.ServiceAdvisorId.Value);
                if (advisorExists)
                {
                    return serviceOrder.ServiceAdvisorId.Value;
                }
            }

            var firstStaffId = await _context.Staffs
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            if (firstStaffId <= 0)
            {
                throw new InvalidOperationException("Không có nhân sự trong hệ thống để khởi tạo yêu cầu chẩn đoán.");
            }

            return firstStaffId;
        }

        private async Task<bool> HasSignedCheckInAsync(long serviceOrderId)
        {
            return await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_IN" &&
                    x.CustomerSignatureUrl != null &&
                    x.CustomerSignatureUrl != "");
        }
    }
}


