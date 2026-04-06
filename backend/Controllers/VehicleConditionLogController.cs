using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        private const string LogTypeCheckIn = "CHECK_IN";
        private const string LogTypeCheckOut = "CHECK_OUT";

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

            var logType = NormalizeLogType(itemDto.LogType);
            if (logType == LogTypeCheckOut)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return BadRequest(new { message = "Chỉ được tạo CHECK_OUT sau khi khách đã ký CHECK_IN." });
                }
            }
            else if (logType == LogTypeCheckIn)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (hasSignedCheckIn)
                {
                    return BadRequest(new { message = "CHECK_IN đã được khách hàng ký xác nhận, không thể tạo mới." });
                }

                if (!string.IsNullOrWhiteSpace(itemDto.CustomerSignatureUrl) &&
                    !HasRequiredCornerImages(itemDto.DefectsAndImages))
                {
                    return BadRequest(new { message = "Cần chụp đủ 4 góc xe (trước trái, trước phải, sau trái, sau phải) trước khi khách ký CHECK_IN." });
                }
            }

            var entity = _mapper.Map<VehicleConditionLog>(itemDto);
            var createdItem = await _vehicleConditionLogService.AddAsync(entity);

            if (logType == LogTypeCheckIn)
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(itemDto.ServiceOrderId, LogTypeCheckIn);
            }
            else if (logType == LogTypeCheckOut)
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(itemDto.ServiceOrderId, LogTypeCheckOut);
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

            if (itemDto == null)
            {
                return BadRequest(new { message = "Dữ liệu kiểm tra xe không hợp lệ." });
            }

            var existingType = NormalizeLogType(existingItem.LogType);
            if (existingType == LogTypeCheckIn && !string.IsNullOrWhiteSpace(existingItem.CustomerSignatureUrl))
            {
                return BadRequest(new { message = "CHECK_IN đã được khách ký, không thể chỉnh sửa." });
            }

            var targetType = NormalizeLogType(itemDto?.LogType);
            if (string.IsNullOrWhiteSpace(targetType))
            {
                targetType = existingType;
            }

            if (targetType == LogTypeCheckOut)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return BadRequest(new { message = "Chỉ được cập nhật CHECK_OUT sau khi khách đã ký CHECK_IN." });
                }
            }

            var effectiveSignature = string.IsNullOrWhiteSpace(itemDto.CustomerSignatureUrl)
                ? existingItem.CustomerSignatureUrl
                : itemDto.CustomerSignatureUrl;

            var effectiveDefects = string.IsNullOrWhiteSpace(itemDto.DefectsAndImages)
                ? existingItem.DefectsAndImages
                : itemDto.DefectsAndImages;

            if (targetType == LogTypeCheckIn &&
                !string.IsNullOrWhiteSpace(effectiveSignature) &&
                !HasRequiredCornerImages(effectiveDefects))
            {
                return BadRequest(new { message = "Cần chụp đủ 4 góc xe (trước trái, trước phải, sau trái, sau phải) trước khi khách ký CHECK_IN." });
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
            _ = serviceOrderId;
            _ = requestType;
            _ = technicianId;
            return BadRequest(new { message = "Yêu cầu CHECK_IN/CHECK_OUT do lễ tân xử lý trực tiếp, không phân công cho kỹ thuật viên." });
        }

        private static string NormalizeLogType(string? logType)
        {
            return (logType ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static bool HasRequiredCornerImages(string? defectsAndImages)
        {
            if (string.IsNullOrWhiteSpace(defectsAndImages))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(defectsAndImages);
                var root = document.RootElement;

                if (TryValidateCornerImagesObject(root))
                {
                    return true;
                }

                var items = ExtractDefectItems(root);
                if (items.Count == 0)
                {
                    return false;
                }

                var hasFrontLeft = false;
                var hasFrontRight = false;
                var hasRearLeft = false;
                var hasRearRight = false;

                foreach (var item in items)
                {
                    if (!TryGetStringProperty(item, "part", out var partText))
                    {
                        continue;
                    }

                    if (!TryGetProperty(item, "images", out var imagesElement) || !HasAnyImage(imagesElement))
                    {
                        continue;
                    }

                    var normalizedPart = NormalizeSearchText(partText);
                    if (!hasFrontLeft && ContainsAny(normalizedPart, "goc truoc trai", "truoc trai", "front left"))
                    {
                        hasFrontLeft = true;
                    }
                    else if (!hasFrontRight && ContainsAny(normalizedPart, "goc truoc phai", "truoc phai", "front right"))
                    {
                        hasFrontRight = true;
                    }
                    else if (!hasRearLeft && ContainsAny(normalizedPart, "goc sau trai", "sau trai", "rear left", "back left"))
                    {
                        hasRearLeft = true;
                    }
                    else if (!hasRearRight && ContainsAny(normalizedPart, "goc sau phai", "sau phai", "rear right", "back right"))
                    {
                        hasRearRight = true;
                    }
                }

                return hasFrontLeft && hasFrontRight && hasRearLeft && hasRearRight;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryValidateCornerImagesObject(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetProperty(root, "cornerImages", out var cornerImages) || cornerImages.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return HasCornerImage(cornerImages, "frontLeft")
                && HasCornerImage(cornerImages, "frontRight")
                && HasCornerImage(cornerImages, "rearLeft")
                && HasCornerImage(cornerImages, "rearRight");
        }

        private static bool HasCornerImage(JsonElement cornerImages, string key)
        {
            return TryGetProperty(cornerImages, key, out var imageElement) && HasAnyImage(imageElement);
        }

        private static List<JsonElement> ExtractDefectItems(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(root, "defectItems", out var defectItems) && defectItems.ValueKind == JsonValueKind.Array)
                {
                    return defectItems.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
                }

                if (TryGetProperty(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    return items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
                }
            }

            return new List<JsonElement>();
        }

        private static bool HasAnyImage(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return !string.IsNullOrWhiteSpace(element.GetString());
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    return true;
                }

                if (item.ValueKind == JsonValueKind.Object &&
                    TryGetProperty(item, "url", out var urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(urlElement.GetString()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!TryGetProperty(element, propertyName, out var propertyElement))
            {
                return false;
            }

            if (propertyElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = propertyElement.GetString() ?? string.Empty;
            return true;
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
        {
            propertyValue = default;
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return tokens.Any(token => source.Contains(token, StringComparison.Ordinal));
        }

        private static string NormalizeSearchText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'd');
        }
    }
}
