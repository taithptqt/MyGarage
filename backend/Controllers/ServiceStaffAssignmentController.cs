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

namespace MyGarage.Controllers
{
    [Route("api/service-staff-assignments")]
    [ApiController]
    public class ServiceStaffAssignmentController : ControllerBase
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "PENDING",
            "IN_PROGRESS",
            "PAUSED",
            "COMPLETED",
        };

        private readonly IServiceStaffAssignmentService _serviceStaffAssignmentService;
        private readonly IVehicleConditionLogService _vehicleConditionLogService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentController(
            IServiceStaffAssignmentService serviceStaffAssignmentService,
            IVehicleConditionLogService vehicleConditionLogService,
            IGarageContext context,
            IMapper mapper)
        {
            _serviceStaffAssignmentService = serviceStaffAssignmentService;
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

            var entity = _mapper.Map<ServiceStaffAssignment>(itemDto);
            var createdItem = await _serviceStaffAssignmentService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceStaffAssignmentResponseDto>> Update(long id, [FromBody] ServiceStaffAssignmentUpdateDto itemDto)
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

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceStaffAssignmentService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ServiceStaffAssignmentResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceStaffAssignmentService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
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
                    dto.Status = "PENDING";
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
                    dto.Status = "PENDING";
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
    }
}






