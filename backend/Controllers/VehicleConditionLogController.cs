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
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
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
        private readonly IDiagnosisService _diagnosisService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;
        private readonly ICloudStorageService _cloudStorageService;

        public VehicleConditionLogController(
            IVehicleConditionLogService vehicleConditionLogService,
            IDiagnosisService diagnosisService,
            IGarageContext context,
            IMapper mapper,
            ICloudStorageService cloudStorageService)
        {
            _vehicleConditionLogService = vehicleConditionLogService;
            _diagnosisService = diagnosisService;
            _context = context;
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
                return BadRequest(new { message = "D? li?u ki?m tra xe không h?p l?." });
            }

            var logType = NormalizeLogType(itemDto.LogType);
            if (logType == LogTypeCheckOut)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return BadRequest(new { message = "Ch? du?c t?o CHECK_OUT sau khi khách dã ký CHECK_IN." });
                }
            }
            else if (logType == LogTypeCheckIn)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(itemDto.ServiceOrderId);
                if (hasSignedCheckIn)
                {
                    return BadRequest(new { message = "CHECK_IN dã du?c khách hàng ký xác nh?n, không th? t?o m?i." });
                }

                if (!string.IsNullOrWhiteSpace(itemDto.CustomerSignatureUrl) &&
                    !HasRequiredCornerImages(itemDto.DefectsAndImages))
                {
                    return BadRequest(new { message = "C?n ch?p d? 4 góc xe (tru?c trái, tru?c ph?i, sau trái, sau ph?i) tru?c khi khách ký CHECK_IN." });
                }
            }

            var entity = _mapper.Map<VehicleConditionLog>(itemDto);
            var createdItem = await _vehicleConditionLogService.AddAsync(entity);

            if (logType == LogTypeCheckIn)
            {
                await _vehicleConditionLogService.MarkActiveRequestCompletedAsync(itemDto.ServiceOrderId, LogTypeCheckIn);
                await TryMarkServiceOrderCheckedInAsync(logType, itemDto.ServiceOrderId, createdItem.CustomerSignatureUrl);
                await TryAutoCreateDiagnosisQueueAfterCheckInAsync(logType, itemDto.ServiceOrderId, createdItem.CustomerSignatureUrl);
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
                return BadRequest(new { message = "D? li?u ki?m tra xe không h?p l?." });
            }
            var updateDto = itemDto;

            var existingType = NormalizeLogType(existingItem.LogType);
            if (existingType == LogTypeCheckIn && !string.IsNullOrWhiteSpace(existingItem.CustomerSignatureUrl))
            {
                return BadRequest(new { message = "CHECK_IN dã du?c khách ký, không th? ch?nh s?a." });
            }

            var targetType = NormalizeLogType(updateDto.LogType);
            if (string.IsNullOrWhiteSpace(targetType))
            {
                targetType = existingType;
            }

            if (targetType == LogTypeCheckOut)
            {
                var hasSignedCheckIn = await _vehicleConditionLogService.HasSignedCheckInAsync(updateDto.ServiceOrderId);
                if (!hasSignedCheckIn)
                {
                    return BadRequest(new { message = "Ch? du?c c?p nh?t CHECK_OUT sau khi khách dã ký CHECK_IN." });
                }
            }

            var effectiveSignature = string.IsNullOrWhiteSpace(updateDto.CustomerSignatureUrl)
                ? existingItem.CustomerSignatureUrl
                : updateDto.CustomerSignatureUrl;

            var effectiveDefects = string.IsNullOrWhiteSpace(updateDto.DefectsAndImages)
                ? existingItem.DefectsAndImages
                : updateDto.DefectsAndImages;

            if (targetType == LogTypeCheckIn &&
                !string.IsNullOrWhiteSpace(effectiveSignature) &&
                !HasRequiredCornerImages(effectiveDefects))
            {
                return BadRequest(new { message = "C?n ch?p d? 4 góc xe (tru?c trái, tru?c ph?i, sau trái, sau ph?i) tru?c khi khách ký CHECK_IN." });
            }

            _mapper.Map(updateDto, existingItem);
            var updatedItem = await _vehicleConditionLogService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            await TryMarkServiceOrderCheckedInAsync(targetType, updatedItem.ServiceOrderId, updatedItem.CustomerSignatureUrl);
            await TryAutoCreateDiagnosisQueueAfterCheckInAsync(targetType, updatedItem.ServiceOrderId, updatedItem.CustomerSignatureUrl);

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
                return BadRequest(new { message = "D? li?u ?nh không h?p l?." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                 !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { message = "Ch? h? tr? t?p ?nh ho?c video." });
            }

            try
            {
                var url = await SaveUploadedFileAsync(file, "vehicle-condition");
                return Ok(new { url });
            }
            catch
            {
                return BadRequest(new { message = "Không th? t?i t?p lên máy ch?." });
            }
        }

        [HttpPost("signatures")]
        public async Task<ActionResult<object>> CreateSignature([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "D? li?u ch? ký không h?p l?." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Ch? h? tr? t?p ?nh cho ch? ký." });
            }

            try
            {
                var url = await SaveUploadedFileAsync(file, "signatures");
                return Ok(new { url });
            }
            catch
            {
                return BadRequest(new { message = "Không th? t?i ch? ký lên máy ch?." });
            }
        }

        private async Task<string> SaveUploadedFileAsync(IFormFile file, string folderName)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".png";
            }

            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
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
                return NotFound(new { message = "Không tìm th?y l?nh s?a ch?a." });
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
                    ? "Xe dã du?c check-in."
                    : "Xe dã du?c checkout.";
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
            return BadRequest(new { message = "Yêu c?u CHECK_IN/CHECK_OUT do l? tân x? lý tr?c ti?p, không phân công cho k? thu?t viên." });
        }

        private static string NormalizeLogType(string? logType)
        {
            return (logType ?? string.Empty).Trim().ToUpperInvariant();
        }

        private async Task TryMarkServiceOrderCheckedInAsync(string? logType, long serviceOrderId, string? customerSignatureUrl)
        {
            if (NormalizeLogType(logType) != LogTypeCheckIn)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(customerSignatureUrl))
            {
                return;
            }

            await _vehicleConditionLogService.MarkServiceOrderCheckedInAsync(serviceOrderId);
        }

        private async Task TryAutoCreateDiagnosisQueueAfterCheckInAsync(string? logType, long serviceOrderId, string? customerSignatureUrl)
        {
            if (NormalizeLogType(logType) != LogTypeCheckIn)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(customerSignatureUrl))
            {
                return;
            }

            try
            {
                var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return;
                }

                var shouldAutoCreateDiagnosis = await ShouldAutoCreateDiagnosisQueueAsync(serviceOrder);
                if (!shouldAutoCreateDiagnosis)
                {
                    return;
                }

                var hasSignedCheckIn = await _diagnosisService.HasSignedCheckInAsync(serviceOrderId);
                if (!hasSignedCheckIn)
                {
                    return;
                }

                var existingDiagnoses = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
                if (existingDiagnoses.Count > 0)
                {
                    return;
                }

                var intakeSuggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
                    serviceOrderId,
                    dispatchTag: null,
                    bufferMinutesOverride: null,
                    requestedPackageCode: null);

                var suggestedTeamId = intakeSuggestion.SuggestedTeamId;
                if (!suggestedTeamId.HasValue || suggestedTeamId.Value <= 0)
                {
                    return;
                }

                var symptoms = ExtractSymptomsFromNotes(serviceOrder.CustomerNotes);
                var resolvedDiagnoseBayId = await _diagnosisService.ResolveDiagnoseBayIdForBranchAsync(
                    serviceOrder.ExecuteBranchId,
                    intakeSuggestion.SuggestedBayId);
                var scheduledSlot = await _diagnosisService.ScheduleDiagnosisQueueSlotAsync(
                    serviceOrderId,
                    intakeSuggestion.PackageEffectiveMinutes,
                    bufferMinutes: 15,
                    preferredBayId: resolvedDiagnoseBayId);
                var finalAssignedBayId = scheduledSlot?.BayId ?? resolvedDiagnoseBayId;
                await _diagnosisService.CreatePendingDiagnosesAsync(
                    serviceOrderId,
                    technicianId: null,
                    symptoms,
                    intakeSuggestion.DispatchTag,
                    assignedTeamId: suggestedTeamId.Value,
                    assignedBayId: finalAssignedBayId);
            }
            catch
            {
                // Keep check-in flow stable even if auto diagnosis bootstrap cannot be created.
            }
        }

        private async Task<bool> ShouldAutoCreateDiagnosisQueueAsync(ServiceOrder serviceOrder)
        {
            var requestedServiceIds = await LoadRequestedServiceIdsFromServiceOrderAsync(serviceOrder.Id);
            if (requestedServiceIds.Count == 0 && serviceOrder.AppointmentId.HasValue)
            {
                var fromAppointment = await LoadRequestedServiceIdsFromAppointmentAsync(serviceOrder.AppointmentId.Value);
                requestedServiceIds = fromAppointment;
            }

            if (requestedServiceIds.Count == 0)
            {
                return await HasInspectionKeywordInServiceOrderDetailsAsync(serviceOrder.Id);
            }

            var serviceRows = await _context.StandardServices
                .AsNoTracking()
                .Where(x => requestedServiceIds.Contains(x.Id))
                .Join(
                    _context.ServiceCategories.AsNoTracking(),
                    service => service.CategoryId,
                    category => category.Id,
                    (service, category) => new
                    {
                        service.Code,
                        service.Name,
                        CategoryCode = category.Code,
                        CategoryName = category.Name
                    })
                .ToListAsync();

            return serviceRows.Any(row => IsInspectionService(row.Code, row.Name, row.CategoryCode, row.CategoryName));
        }

        private async Task<bool> HasInspectionKeywordInServiceOrderDetailsAsync(long serviceOrderId)
        {
            var detailRows = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Select(x => new
                {
                    x.ItemName,
                    x.Description
                })
                .ToListAsync();

            return detailRows.Any(row => IsInspectionService(
                serviceCode: null,
                serviceName: row.ItemName,
                categoryCode: null,
                categoryName: row.Description));
        }

        private async Task<List<long>> LoadRequestedServiceIdsFromServiceOrderAsync(long serviceOrderId)
        {
            return await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.StandardServiceId.HasValue && x.StandardServiceId.Value > 0)
                .Where(x => x.DiagnoseRecommendationId == null)
                .Select(x => x.StandardServiceId!.Value)
                .Distinct()
                .ToListAsync();
        }

        private async Task<List<long>> LoadRequestedServiceIdsFromAppointmentAsync(long appointmentId)
        {
            var requestedServiceIdsRaw = await _context.Appointments
                .AsNoTracking()
                .Where(x => x.Id == appointmentId)
                .Select(x => x.RequestedServiceIds)
                .FirstOrDefaultAsync();

            return ParseRequestedServiceIds(requestedServiceIdsRaw);
        }

        private static List<long> ParseRequestedServiceIds(string? requestedServiceIds)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
            {
                return new List<long>();
            }

            return requestedServiceIds
                .Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => long.TryParse(token.Trim(), out var parsedId) ? parsedId : 0L)
                .Where(parsedId => parsedId > 0)
                .Distinct()
                .ToList();
        }

        private static bool IsInspectionService(string? serviceCode, string? serviceName, string? categoryCode, string? categoryName)
        {
            _ = serviceName;
            _ = categoryCode;
            _ = categoryName;

            // Strict rule per business requirement: only 7 diagnosis package codes trigger auto diagnosis queue.
            var normalizedCode = NormalizeSearchText(serviceCode)
                .Replace("_", "-", StringComparison.Ordinal)
                .Trim()
                .ToUpperInvariant();

            return normalizedCode is "DG-MINI" or "DG-GEN" or "DG-SCAN" or "DG-ENG" or "DG-ELEC" or "DG-AC" or "DG-NOISE";
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
                using var document = JsonDocument.Parse(notes);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var values = root.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => (item.GetString() ?? string.Empty).Trim())
                        .Where(IsMeaningfulSymptom)
                        .Distinct()
                        .ToList();
                    return values.Count > 0 ? values : fallback;
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("symptoms", out var symptomsElement) && symptomsElement.ValueKind == JsonValueKind.Array)
                    {
                        var values = symptomsElement.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => (item.GetString() ?? string.Empty).Trim())
                            .Where(IsMeaningfulSymptom)
                            .Distinct()
                            .ToList();
                        return values.Count > 0 ? values : fallback;
                    }

                    if (root.TryGetProperty("note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String)
                    {
                        var note = (noteElement.GetString() ?? string.Empty).Trim();
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

        private static bool IsMeaningfulSymptom(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizeSearchText(value);
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
                .Replace('\u0111', 'd')
                .Replace('\u0110', 'd');
        }
    }
}



