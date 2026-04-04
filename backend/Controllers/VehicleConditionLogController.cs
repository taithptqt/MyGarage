using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/vehicle-condition-logs")]
    [ApiController]
    public class VehicleConditionLogController : ControllerBase
    {
        public sealed record ConditionRequestCreateDto
        {
            public long ServiceOrderId { get; init; }
            public string? RequestType { get; init; }
        }

        public sealed record ConditionRequestAssignmentDto
        {
            public string? RequestType { get; init; }
            public long TechnicianId { get; init; }
        }

        private readonly IVehicleConditionLogService _vehicleConditionLogService;
        private readonly IMapper _mapper;
        private readonly ICloudStorageService _cloudStorageService;

        public VehicleConditionLogController(
            IVehicleConditionLogService vehicleConditionLogService,
            IMapper mapper,
            ICloudStorageService cloudStorageService)
        {
            _vehicleConditionLogService = vehicleConditionLogService;
            _mapper = mapper;
            _cloudStorageService = cloudStorageService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            var items = serviceOrderId.HasValue
                ? await _vehicleConditionLogService.GetByOrderAsync(serviceOrderId.Value)
                : await _vehicleConditionLogService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> GetById(long id)
        {
            var item = await _vehicleConditionLogService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<VehicleConditionLogResponseDto>(item);
            return Ok(dto);
        }

        [HttpGet("condition-jobs")]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetRequests([FromQuery] string? requestType = null)
        {
            var type = (requestType ?? "CHECK_IN").Trim().ToUpperInvariant();
            if (type != "CHECK_IN" && type != "CHECK_OUT")
            {
                return BadRequest(new { message = "requestType must be CHECK_IN or CHECK_OUT." });
            }

            var activeRequests = await _vehicleConditionLogService.GetActiveRequestsAsync(type);
            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(activeRequests);
            return Ok(dtos);
        }

        [HttpPost("condition-jobs")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> CreateRequest([FromBody] ConditionRequestCreateDto request)
        {
            var type = (request.RequestType ?? string.Empty).Trim().ToUpperInvariant();
            if (type == "CHECK_IN")
            {
                return await CreateRequestByTypeAsync(request.ServiceOrderId, "CHECK_IN");
            }

            if (type == "CHECK_OUT")
            {
                return await CreateRequestByTypeAsync(request.ServiceOrderId, "CHECK_OUT");
            }

            return BadRequest(new { message = "requestType must be CHECK_IN or CHECK_OUT." });
        }

        [HttpPatch("condition-jobs/{serviceOrderId:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> PatchRequestAssignment(
            long serviceOrderId,
            [FromBody] ConditionRequestAssignmentDto request)
        {
            var type = (request.RequestType ?? string.Empty).Trim().ToUpperInvariant();
            if (type == "CHECK_IN")
            {
                return await AssignRequestByTypeAsync(serviceOrderId, "CHECK_IN", request.TechnicianId);
            }

            if (type == "CHECK_OUT")
            {
                return await AssignRequestByTypeAsync(serviceOrderId, "CHECK_OUT", request.TechnicianId);
            }

            return BadRequest(new { message = "requestType must be CHECK_IN or CHECK_OUT." });
        }

        [HttpPost]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> Create([FromBody] VehicleConditionLogCreateDto itemDto)
        {
            if (itemDto == null)
            {
                return BadRequest(new { message = "Dữ liệu kiểm tra xe không hợp lệ." });
            }

            var logType = (itemDto.LogType ?? string.Empty).Trim().ToUpperInvariant();
            if (logType == "CHECK_OUT")
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return BadRequest(new { message = "Chỉ được tạo CHECK_OUT sau khi khách đã ký CHECK_IN." });
                }
            }

            var entity = _mapper.Map<VehicleConditionLog>(itemDto);
            var createdItem = await _vehicleConditionLogService.AddAsync(entity);

            if (string.Equals(itemDto.LogType, "CHECK_IN", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(itemDto.ServiceOrderId, "CHECK_IN");
            }
            else if (string.Equals(itemDto.LogType, "CHECK_OUT", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(itemDto.ServiceOrderId, "CHECK_OUT");
            }

            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> Update(long id, [FromBody] VehicleConditionLogUpdateDto itemDto)
        {
            var existingItem = await _vehicleConditionLogService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _vehicleConditionLogService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleConditionLogService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPost("attachments")]
        public async Task<ActionResult<object>> UploadAttachment([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Dữ liệu ảnh không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                 !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ tệp ảnh hoặc video." });
            }

            try
            {
                var url = await SaveUploadedFileAsync(file, "vehicle-condition");
                return Ok(new { url });
            }
            catch
            {
                return BadRequest(new { message = "Không thể tải tệp lên máy chủ." });
            }
        }

        [HttpPost("signatures")]
        public async Task<ActionResult<object>> CreateSignature([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Dữ liệu chữ ký không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ tệp ảnh cho chữ ký." });
            }

            try
            {
                var url = await SaveUploadedFileAsync(file, "signatures");
                return Ok(new { url });
            }
            catch
            {
                return BadRequest(new { message = "Không thể tải chữ ký lên máy chủ." });
            }
        }

        private async Task<string> SaveUploadedFileAsync(IFormFile file, string folderName)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".png";
            }

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            await using var stream = file.OpenReadStream();
            return await _cloudStorageService.UploadAsync(
                stream,
                folderName,
                fileName,
                file.ContentType,
                HttpContext.RequestAborted);
        }

        private async Task<ActionResult<VehicleConditionLogResponseDto>> CreateRequestByTypeAsync(long serviceOrderId, string requestType)
        {
            var orderExists = await _vehicleConditionLogService.ServiceOrderExistsAsync(serviceOrderId);
            if (!orderExists)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var existing = await _vehicleConditionLogService.GetActiveRequestAsync(serviceOrderId, requestType);
            if (existing != null)
            {
                return Ok(_mapper.Map<VehicleConditionLogResponseDto>(existing));
            }

            var hasCompletedLog = await _vehicleConditionLogService.HasCompletedLogAsync(serviceOrderId, requestType);
            if (hasCompletedLog)
            {
                var message = requestType == "CHECK_IN"
                    ? "Xe đã được check-in."
                    : "Xe đã được checkout.";
                return BadRequest(new { message });
            }

            var requestLog = await _vehicleConditionLogService.CreateRequestAsync(serviceOrderId, requestType);
            return Ok(_mapper.Map<VehicleConditionLogResponseDto>(requestLog));
        }

        private async Task<ActionResult<VehicleConditionLogResponseDto>> AssignRequestByTypeAsync(long serviceOrderId, string requestType, long technicianId)
        {
            if (technicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu kỹ thuật viên nhận yêu cầu." });
            }

            var requestLog = await _vehicleConditionLogService.GetActiveRequestAsync(serviceOrderId, requestType);
            if (requestLog == null)
            {
                var message = requestType == "CHECK_IN"
                    ? "Không tìm thấy yêu cầu check-in."
                    : "Không tìm thấy yêu cầu checkout.";
                return NotFound(new { message });
            }

            if (requestLog.DashboardWarningLights == nameof(ConditionRequestStatusCode.COMPLETED))
            {
                var message = requestType == "CHECK_IN"
                    ? "Yêu cầu check-in đã hoàn tất."
                    : "Yêu cầu checkout đã hoàn tất.";
                return BadRequest(new { message });
            }

            if (requestLog.DashboardWarningLights == nameof(ConditionRequestStatusCode.ASSIGNED) &&
                requestLog.InspectorId.HasValue &&
                requestLog.InspectorId.Value != technicianId)
            {
                var message = requestType == "CHECK_IN"
                    ? "Yêu cầu check-in đã được kỹ thuật viên khác nhận."
                    : "Yêu cầu checkout đã được kỹ thuật viên khác nhận.";
                return Conflict(new { message });
            }

            var techExists = await _vehicleConditionLogService.TechnicianExistsAsync(technicianId);
            if (!techExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            await _vehicleConditionLogService.MarkRequestAssignedAsync(requestLog, technicianId);
            return Ok(_mapper.Map<VehicleConditionLogResponseDto>(requestLog));
        }
    }
}
