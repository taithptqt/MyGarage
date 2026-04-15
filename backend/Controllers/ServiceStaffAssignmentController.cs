using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}






