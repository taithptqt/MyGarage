using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleConditionLogController : ControllerBase
    {
        private readonly IVehicleConditionLogService _vehicleConditionLogService;
        private readonly IMapper _mapper;
        private readonly GarageContext _context;
        private readonly ICloudStorageService _cloudStorageService;

        public VehicleConditionLogController(
            IVehicleConditionLogService vehicleConditionLogService,
            IMapper mapper,
            GarageContext context,
            ICloudStorageService cloudStorageService)
        {
            _vehicleConditionLogService = vehicleConditionLogService;
            _mapper = mapper;
            _context = context;
            _cloudStorageService = cloudStorageService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetAll()
        {
            var items = await _vehicleConditionLogService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
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

        [HttpGet("by-order/{serviceOrderId:long}")]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetByOrder(long serviceOrderId)
        {
            var items = await _context.VehicleConditionLogs
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();

            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("checkin-requests")]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetCheckInRequests()
        {
            var activeRequests = await _context.VehicleConditionLogs
                .Where(x => x.LogType == "CHECK_IN")
                .Where(x => x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED")
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();

            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(activeRequests);
            return Ok(dtos);
        }

        [HttpGet("checkout-requests")]
        public async Task<ActionResult<IEnumerable<VehicleConditionLogResponseDto>>> GetCheckOutRequests()
        {
            var activeRequests = await _context.VehicleConditionLogs
                .Where(x => x.LogType == "CHECK_OUT")
                .Where(x => x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED")
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();

            var dtos = _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(activeRequests);
            return Ok(dtos);
        }

        [HttpPost("checkin-requests/{serviceOrderId:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> CreateCheckInRequest(long serviceOrderId)
        {
            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var existing = await _context.VehicleConditionLogs
                .FirstOrDefaultAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_IN" &&
                    (x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED"));

            if (existing != null)
            {
                var existingDto = _mapper.Map<VehicleConditionLogResponseDto>(existing);
                return Ok(existingDto);
            }

            var hasCheckInLog = await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_IN" &&
                    x.DashboardWarningLights != "PENDING" &&
                    x.DashboardWarningLights != "ASSIGNED");
            if (hasCheckInLog)
            {
                return BadRequest(new { message = "Xe đã được check-in." });
            }

            var requestLog = new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId,
                LogType = "CHECK_IN",
                DashboardWarningLights = "PENDING",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.VehicleConditionLogs.Add(requestLog);
            await _context.SaveChangesAsync();

            var dto = _mapper.Map<VehicleConditionLogResponseDto>(requestLog);
            return Ok(dto);
        }

        [HttpPost("checkout-requests/{serviceOrderId:long}")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> CreateCheckOutRequest(long serviceOrderId)
        {
            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var existing = await _context.VehicleConditionLogs
                .FirstOrDefaultAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_OUT" &&
                    (x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED"));

            if (existing != null)
            {
                var existingDto = _mapper.Map<VehicleConditionLogResponseDto>(existing);
                return Ok(existingDto);
            }

            var hasCheckoutLog = await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_OUT" &&
                    x.DashboardWarningLights != "PENDING" &&
                    x.DashboardWarningLights != "ASSIGNED");
            if (hasCheckoutLog)
            {
                return BadRequest(new { message = "Xe đã được checkout." });
            }

            var requestLog = new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId,
                LogType = "CHECK_OUT",
                DashboardWarningLights = "PENDING",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.VehicleConditionLogs.Add(requestLog);
            await _context.SaveChangesAsync();

            var dto = _mapper.Map<VehicleConditionLogResponseDto>(requestLog);
            return Ok(dto);
        }

        [HttpPost("checkin-requests/{serviceOrderId:long}/accept")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> AcceptCheckInRequest(
            long serviceOrderId,
            [FromBody] CheckInRequestAcceptDto request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu kỹ thuật viên nhận yêu cầu." });
            }

            var checkInRequest = await _context.VehicleConditionLogs
                .Where(x => x.ServiceOrderId == serviceOrderId && x.LogType == "CHECK_IN")
                .Where(x => x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED")
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .FirstOrDefaultAsync();

            if (checkInRequest == null)
            {
                return NotFound(new { message = "Không tìm thấy yêu cầu check-in." });
            }

            if (checkInRequest.DashboardWarningLights == "COMPLETED")
            {
                return BadRequest(new { message = "Yêu cầu check-in đã hoàn tất." });
            }

            if (checkInRequest.DashboardWarningLights == "ASSIGNED" &&
                checkInRequest.InspectorId.HasValue &&
                checkInRequest.InspectorId.Value != request.TechnicianId)
            {
                return Conflict(new { message = "Yêu cầu check-in đã được kỹ thuật viên khác nhận." });
            }

            var techExists = await _context.Staffs.AnyAsync(x => x.Id == request.TechnicianId);
            if (!techExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            checkInRequest.InspectorId = request.TechnicianId;
            checkInRequest.DashboardWarningLights = "ASSIGNED";
            checkInRequest.UpdatedAt = DateTime.UtcNow;

            _context.VehicleConditionLogs.Update(checkInRequest);
            await _context.SaveChangesAsync();

            var dto = _mapper.Map<VehicleConditionLogResponseDto>(checkInRequest);
            return Ok(dto);
        }

        [HttpPost("checkout-requests/{serviceOrderId:long}/accept")]
        public async Task<ActionResult<VehicleConditionLogResponseDto>> AcceptCheckOutRequest(
            long serviceOrderId,
            [FromBody] CheckInRequestAcceptDto request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return BadRequest(new { message = "Thiếu kỹ thuật viên nhận yêu cầu." });
            }

            var checkOutRequest = await _context.VehicleConditionLogs
                .Where(x => x.ServiceOrderId == serviceOrderId && x.LogType == "CHECK_OUT")
                .Where(x => x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED")
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .FirstOrDefaultAsync();

            if (checkOutRequest == null)
            {
                return NotFound(new { message = "Không tìm thấy yêu cầu checkout." });
            }

            if (checkOutRequest.DashboardWarningLights == "COMPLETED")
            {
                return BadRequest(new { message = "Yêu cầu checkout đã hoàn tất." });
            }

            if (checkOutRequest.DashboardWarningLights == "ASSIGNED" &&
                checkOutRequest.InspectorId.HasValue &&
                checkOutRequest.InspectorId.Value != request.TechnicianId)
            {
                return Conflict(new { message = "Yêu cầu checkout đã được kỹ thuật viên khác nhận." });
            }

            var techExists = await _context.Staffs.AnyAsync(x => x.Id == request.TechnicianId);
            if (!techExists)
            {
                return BadRequest(new { message = "Kỹ thuật viên không tồn tại." });
            }

            checkOutRequest.InspectorId = request.TechnicianId;
            checkOutRequest.DashboardWarningLights = "ASSIGNED";
            checkOutRequest.UpdatedAt = DateTime.UtcNow;

            _context.VehicleConditionLogs.Update(checkOutRequest);
            await _context.SaveChangesAsync();

            var dto = _mapper.Map<VehicleConditionLogResponseDto>(checkOutRequest);
            return Ok(dto);
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
                var hasSignedCheckIn = await HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return BadRequest(new { message = "Chỉ được tạo CHECK_OUT sau khi khách đã ký CHECK_IN." });
                }
            }

            var entity = _mapper.Map<VehicleConditionLog>(itemDto);
            var createdItem = await _vehicleConditionLogService.AddAsync(entity);

            if (string.Equals(itemDto.LogType, "CHECK_IN", StringComparison.OrdinalIgnoreCase))
            {
                var activeRequest = await _context.VehicleConditionLogs
                    .Where(x => x.ServiceOrderId == itemDto.ServiceOrderId && x.LogType == "CHECK_IN")
                    .Where(x => x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED")
                    .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                    .FirstOrDefaultAsync();

                if (activeRequest != null)
                {
                    activeRequest.DashboardWarningLights = "COMPLETED";
                    activeRequest.UpdatedAt = DateTime.UtcNow;
                    _context.VehicleConditionLogs.Update(activeRequest);
                    await _context.SaveChangesAsync();
                }
            }
            else if (string.Equals(itemDto.LogType, "CHECK_OUT", StringComparison.OrdinalIgnoreCase))
            {
                var activeRequest = await _context.VehicleConditionLogs
                    .Where(x => x.ServiceOrderId == itemDto.ServiceOrderId && x.LogType == "CHECK_OUT")
                    .Where(x => x.DashboardWarningLights == "PENDING" || x.DashboardWarningLights == "ASSIGNED")
                    .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                    .FirstOrDefaultAsync();

                if (activeRequest != null)
                {
                    activeRequest.DashboardWarningLights = "COMPLETED";
                    activeRequest.UpdatedAt = DateTime.UtcNow;
                    _context.VehicleConditionLogs.Update(activeRequest);
                    await _context.SaveChangesAsync();
                }
            }

            var responseDto = _mapper.Map<VehicleConditionLogResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _vehicleConditionLogService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("upload-image")]
        public async Task<ActionResult<object>> UploadImage([FromForm] IFormFile file)
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

        [HttpPost("upload-signature")]
        public async Task<ActionResult<object>> UploadSignature([FromForm] IFormFile file)
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

        private async Task<bool> HasSignedCheckInAsync(long serviceOrderId)
        {
            return await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_IN" &&
                    x.CustomerSignatureUrl != null &&
                    x.CustomerSignatureUrl != "");
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
    }
}
