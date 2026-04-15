using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;

namespace MyGarage.Controllers
{
    [Route("api/service-staff-assignments")]
    [ApiController]
    public class ServiceStaffAssignmentController : ControllerBase
    {
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
        public async Task<ActionResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> GetAll()
        {
            var items = await _serviceStaffAssignmentService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceStaffAssignmentResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> GetById(long id)
        {
            var item = await _serviceStaffAssignmentService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceStaffAssignmentResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Create([FromBody] ServiceStaffAssignmentCreateDto itemDto)
        {
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

                await _serviceOrderService.EnsureAssignmentEditableAsync(serviceOrderId);

                var entity = _mapper.Map<ServiceStaffAssignment>(itemDto);
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
            try
            {
                if (request == null || request.ServiceOrderDetailId <= 0 || request.Assignments == null || request.Assignments.Count == 0)
                {
                    return BadRequest(new { message = "Thiếu dữ liệu phân công nhiều thợ." });
                }

                var checkInValidationError = await ValidateSignedCheckInBeforeAssignmentAsync(request.ServiceOrderDetailId);
                if (!string.IsNullOrWhiteSpace(checkInValidationError))
                {
                    return BadRequest(new { message = checkInValidationError });
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDetailIdAsync(request.ServiceOrderDetailId);
                if (serviceOrderId <= 0)
                {
                    return BadRequest(new { message = "Không tìm thấy service order tương ứng với hạng mục được phân công." });
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
                    return BadRequest(new { message = "Không tìm thấy hạng mục dịch vụ cần phân công." });
                }

                var technicianIds = request.Assignments
                    .Where(x => x != null && x.TechnicianId > 0)
                    .Select(x => x.TechnicianId)
                    .Distinct()
                    .ToList();
                if (technicianIds.Count == 0)
                {
                    return BadRequest(new { message = "Danh sách kỹ thuật viên không hợp lệ." });
                }

                var duplicateTechnicians = request.Assignments
                    .Where(x => x != null && x.TechnicianId > 0)
                    .GroupBy(x => x.TechnicianId)
                    .Where(x => x.Count() > 1)
                    .Select(x => x.Key)
                    .ToList();
                if (duplicateTechnicians.Count > 0)
                {
                    return BadRequest(new { message = $"Kỹ thuật viên bị trùng trong cùng hạng mục: {string.Join(", ", duplicateTechnicians)}." });
                }

                var existingAssignedTechnicianIds = await _context.ServiceStaffAssignments
                    .AsNoTracking()
                    .Where(x => x.ServiceOrderDetailId == request.ServiceOrderDetailId && technicianIds.Contains(x.TechnicianId))
                    .Select(x => x.TechnicianId)
                    .Distinct()
                    .ToListAsync();
                if (existingAssignedTechnicianIds.Count > 0)
                {
                    return BadRequest(new { message = $"Kỹ thuật viên đã được giao trước đó: {string.Join(", ", existingAssignedTechnicianIds)}." });
                }

                var technicians = await _context.Staffs
                    .AsNoTracking()
                    .Where(x => technicianIds.Contains(x.Id) && x.IsDeleted != true)
                    .Select(x => new
                    {
                        x.Id,
                        x.FullName,
                        x.Specialty
                    })
                    .ToDictionaryAsync(x => x.Id, x => x);

                var missingTechnicianIds = technicianIds.Where(x => !technicians.ContainsKey(x)).ToList();
                if (missingTechnicianIds.Count > 0)
                {
                    return BadRequest(new { message = $"Không tìm thấy kỹ thuật viên: {string.Join(", ", missingTechnicianIds)}." });
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
                        return BadRequest(new
                        {
                            message = $"Chuyên môn kỹ thuật viên không phù hợp hạng mục ({requiredSpecialty}).",
                            invalidTechnicians = invalidSpecialtyRows
                        });
                    }
                }

                var splitRatios = new List<decimal>();
                foreach (var assignment in request.Assignments)
                {
                    var ratio = assignment.SplitRatio ?? (1m / request.Assignments.Count);
                    if (ratio <= 0m || ratio > 1m)
                    {
                        return BadRequest(new { message = $"SplitRatio không hợp lệ cho kỹ thuật viên {assignment.TechnicianId}." });
                    }
                    splitRatios.Add(ratio);
                }

                var totalRatio = splitRatios.Sum();
                if (totalRatio > 1.0001m)
                {
                    return BadRequest(new { message = $"Tổng SplitRatio vượt quá 1.0 ({totalRatio:0.####})." });
                }

                var actorId = request.AssignedBy ?? this.GetCurrentUserId();
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
                        return BadRequest(new { message = $"Giá trị status không hợp lệ: {item.Status}. Chấp nhận: {allowed}." });
                    }

                    var proofImageUrl = NormalizeProofImageUrl(item.ProofImageUrl);
                    var requiresInsuranceProof = item.RequiresInsuranceProof ||
                        string.Equals(detailInfo.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(detailInfo.PayerType, "SPLIT", StringComparison.OrdinalIgnoreCase);
                    var completionValidationError = ValidateInsuranceProofOnComplete(status, requiresInsuranceProof, proofImageUrl);
                    if (!string.IsNullOrWhiteSpace(completionValidationError))
                    {
                        return BadRequest(new { message = completionValidationError });
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

                _context.ServiceStaffAssignments.AddRange(createdRows);
                await _context.SaveChangesAsync();

                var response = _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(createdRows);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Update(long id, [FromBody] ServiceStaffAssignmentUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _serviceStaffAssignmentService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
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






