using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/service-orders")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ServiceOrderController : ControllerBase
    {
        private const long MaxInsuranceAssetBytes = 10 * 1024 * 1024;

        private static readonly HashSet<string> AllowedServiceOrderStatuses =
            new(Enum.GetNames<ServiceOrderLifecycleStatus>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedPaymentStatuses =
            new(Enum.GetNames<ServiceOrderPaymentStatusCode>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedDepositStatuses =
            new(Enum.GetNames<ServiceOrderDepositStatusCode>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedQcStatuses =
            new(Enum.GetNames<ServiceOrderQcStatusCode>(), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> AllowedOrderCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "QUICK_SERVICE",
            "GENERAL_REPAIR",
            "MAINTENANCE",
            "INSURANCE_REPAIR",
            "REWORK",
            "WARRANTLY",
        };

        private static readonly JsonSerializerOptions EditRequestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IServiceOrderService _serviceOrderService;
        private readonly IAuditLogService _auditLogService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ServiceOrderController(
            IServiceOrderService serviceOrderService,
            IAuditLogService auditLogService,
            ICloudStorageService cloudStorageService,
            IGarageContext context,
            IMapper mapper)
        {
            _serviceOrderService = serviceOrderService;
            _auditLogService = auditLogService;
            _cloudStorageService = cloudStorageService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderResponseDto>>> GetAll()
        {
            var items = await _serviceOrderService.GetAllAsync();
            var dtos = await MapServiceOrderResponsesAsync(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = await MapServiceOrderResponseAsync(item);
            return Ok(dto);
        }

        [HttpGet("{id}/edit-capabilities")]
        public async Task<ActionResult<ServiceOrderEditCapabilityResponseDto>> GetEditCapabilities(long id)
        {
            var capability = await _serviceOrderService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return NotFound();
            }

            return Ok(capability);
        }

        [Authorize]
        [HttpGet("{id}/edit-requests")]
        public async Task<IActionResult> GetEditRequests(long id, [FromQuery] string? status = null)
        {
            var normalizedStatus = NormalizeToken(status);

            var requestLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.Action == "REQUEST_EDIT" &&
                    log.TableName == "SERVICEORDERS" &&
                    log.RecordId == id)
                .OrderByDescending(log => log.Id)
                .Take(100)
                .ToListAsync();

            var result = requestLogs
                .Select(log => new
                {
                    log.Id,
                    log.UserId,
                    log.BranchId,
                    log.Timestamp,
                    Payload = DeserializeEditRequestPayload(log.NewValue)
                })
                .Where(item => item.Payload != null)
                .Where(item => string.IsNullOrWhiteSpace(normalizedStatus) || NormalizeToken(item.Payload!.Status) == normalizedStatus)
                .Select(item => new
                {
                    requestId = item.Id,
                    item.UserId,
                    item.BranchId,
                    item.Timestamp,
                    section = item.Payload!.Section,
                    reason = item.Payload.Reason,
                    status = item.Payload.Status,
                    requestedByUserId = item.Payload.RequestedByUserId,
                    requestedAtUtc = item.Payload.RequestedAtUtc,
                    decidedByUserId = item.Payload.DecidedByUserId,
                    decidedAtUtc = item.Payload.DecidedAtUtc,
                    decisionNote = item.Payload.DecisionNote,
                    decidedByRoleCode = item.Payload.DecidedByRoleCode,
                    decidedByRoleName = item.Payload.DecidedByRoleName,
                    overrideUntilUtc = item.Payload.OverrideUntilUtc
                })
                .ToList();

            return Ok(result);
        }

        [Authorize]
        [HttpPost("{id}/edit-requests")]
        public async Task<IActionResult> RequestEdit(long id, [FromBody] EditRequestCreateDto? dto)
        {
            var capability = await _serviceOrderService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return NotFound(new { message = "Không tìm thấy service order." });
            }

            if (capability.CanEditWorkshop)
            {
                return BadRequest(new { message = "Service order hiện chưa bị khóa, không cần gửi yêu cầu sửa." });
            }

            var reason = (dto?.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Vui lòng nhập lý do yêu cầu sửa." });
            }

            var actorUserId = this.GetCurrentUserId() ?? dto?.RequestedByUserId;
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var requestedSection = NormalizeSection(dto?.Section, "WORKSHOP");
            var order = await _serviceOrderService.GetByIdAsync(id);
            var now = VietnamTime.Now;
            var payload = new EditRequestPayload
            {
                Section = requestedSection,
                Reason = reason,
                Status = "PENDING",
                RequestedByUserId = actorUserId.Value,
                RequestedAtUtc = now
            };

            var created = await _auditLogService.AddAsync(new AuditLog
            {
                UserId = actorUserId.Value,
                UserType = "STAFF",
                BranchId = order?.ExecuteBranchId,
                Action = "REQUEST_EDIT",
                TableName = "SERVICEORDERS",
                RecordId = id,
                OldValue = JsonSerializer.Serialize(capability),
                NewValue = JsonSerializer.Serialize(payload),
                Timestamp = now
            });

            return Ok(new
            {
                message = "Đã ghi nhận yêu cầu sửa service order.",
                requestId = created.Id
            });
        }

        [Authorize]
        [HttpPatch("{id}/edit-requests/{requestId:long}")]
        public async Task<IActionResult> DecideEditRequest(long id, long requestId, [FromBody] EditRequestDecisionDto? dto)
        {
            var decision = NormalizeToken(dto?.Decision);
            if (decision is not ("APPROVE" or "REJECT"))
            {
                return BadRequest(new { message = "Decision phải là APPROVE hoặc REJECT." });
            }

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var requestLog = await _auditLogService.GetByIdAsync(requestId);
            if (requestLog == null ||
                !string.Equals(requestLog.Action, "REQUEST_EDIT", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(requestLog.TableName, "SERVICEORDERS", StringComparison.OrdinalIgnoreCase) ||
                requestLog.RecordId != id)
            {
                return NotFound(new { message = "Không tìm thấy yêu cầu sửa cần xử lý." });
            }

            var payload = DeserializeEditRequestPayload(requestLog.NewValue);
            if (payload == null)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu sửa không hợp lệ." });
            }

            var currentStatus = NormalizeToken(payload.Status);
            if (!string.Equals(currentStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Yêu cầu sửa này đã được xử lý trước đó." });
            }

            var requestedSection = NormalizeSection(payload.Section, "WORKSHOP");
            var (roleCode, roleName) = await ResolveRoleInfoAsync(actorUserId.Value);
            if (!CanApproveServiceOrderSection(requestedSection, roleCode, roleName))
            {
                return Forbid();
            }

            var note = string.IsNullOrWhiteSpace(dto?.Note) ? null : dto!.Note!.Trim();
            if (decision == "REJECT" && string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối yêu cầu sửa." });
            }

            var now = VietnamTime.Now;
            payload.Section = requestedSection;
            payload.Status = decision == "APPROVE" ? "APPROVED" : "REJECTED";
            payload.DecisionNote = note;
            payload.DecidedByUserId = actorUserId.Value;
            payload.DecidedAtUtc = now;
            payload.DecidedByRoleCode = string.IsNullOrWhiteSpace(roleCode) ? null : roleCode;
            payload.DecidedByRoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName;
            payload.OverrideUntilUtc = decision == "APPROVE" ? now.AddHours(2) : null;

            var beforeDecision = requestLog.NewValue;
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                await using var transaction = await TryBeginTransactionAsync();
                try
                {
                requestLog.NewValue = JsonSerializer.Serialize(payload);
                await _auditLogService.UpdateAsync(requestLog.Id, requestLog);

                await _auditLogService.AddAsync(new AuditLog
                {
                    UserId = actorUserId.Value,
                    UserType = "STAFF",
                    BranchId = requestLog.BranchId,
                    Action = "DECIDE_EDIT_REQUEST",
                    TableName = requestLog.TableName,
                    RecordId = requestLog.RecordId,
                    OldValue = beforeDecision,
                    NewValue = requestLog.NewValue,
                    Timestamp = now
                });

                    if (transaction != null) await transaction.CommitAsync();
                }
                catch
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    throw;
                }

                return Ok(new
                {
                    requestId = requestLog.Id,
                    decision = payload.Status,
                    overrideUntilUtc = payload.OverrideUntilUtc,
                    message = decision == "APPROVE"
                        ? "Đã duyệt yêu cầu sửa service order."
                        : "Đã từ chối yêu cầu sửa service order."
                });
            });
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderResponseDto>> Create([FromBody] ServiceOrderCreateDto itemDto)
        {
            var actorUserId = this.GetCurrentUserId();
            var validationError = NormalizeAndValidateFields(itemDto);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new { message = validationError });
            }

            var entity = _mapper.Map<ServiceOrder>(itemDto);
            entity.Status = nameof(ServiceOrderLifecycleStatus.NEW);
            entity.CreatedBy = actorUserId;
            entity.UpdatedBy = actorUserId;
            var createdItem = await _serviceOrderService.AddAsync(entity);
            var responseDto = await MapServiceOrderResponseAsync(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPost("batches")]
        public async Task<ActionResult<ServiceOrderBatchCreateResponseDto>> CreateBatch([FromBody] ServiceOrderBatchCreateRequestDto dto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                var result = await _serviceOrderService.AddWorkshopBatchAsync(actorUserId, dto);
                var mappedOrders = await MapServiceOrderResponsesAsync(result.Orders);
                var response = new ServiceOrderBatchCreateResponseDto
                {
                    GroupSessionId = result.GroupSessionId,
                    TotalCreated = mappedOrders.Count,
                    Orders = mappedOrders
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/workshop")]
        public async Task<ActionResult<ServiceOrderResponseDto>> UpdateWorkshop(long id, [FromBody] WorkshopServiceOrderEditRequestDto dto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                var updatedItem = await _serviceOrderService.UpdateWorkshopAsync(id, dto, actorUserId);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = await MapServiceOrderResponseAsync(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/handover-readiness")]
        public async Task<ActionResult<HandoverReadinessResponseDto>> GetHandoverReadiness(long id)
        {
            var readiness = await _serviceOrderService.GetHandoverReadinessAsync(id);
            if (readiness == null) return NotFound();
            return Ok(readiness);
        }

        [HttpPost("{id}/handover")]
        public async Task<IActionResult> ProcessHandover(long id, [FromBody] HandoverRequestDto request)
        {
            try
            {
                var result = await _serviceOrderService.ProcessHandoverAsync(id, request);
                if (!result) return NotFound();
                return Ok(new { message = "Vehicle handover completed successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderResponseDto>> Update(long id, [FromBody] ServiceOrderUpdateDto itemDto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                var existingItem = await _serviceOrderService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(id);

                var validationError = NormalizeAndValidateFields(itemDto);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return BadRequest(new { message = validationError });
                }

                _mapper.Map(itemDto, existingItem);
                existingItem.UpdatedBy = actorUserId;
                var updatedItem = await _serviceOrderService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = await MapServiceOrderResponseAsync(updatedItem);
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
                var existingItem = await _serviceOrderService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(id);

                var result = await _serviceOrderService.DeleteAsync(id);
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

        [Authorize]
        [HttpPost("insurance-assets")]
        public async Task<ActionResult<object>> UploadInsuranceAsset([FromForm] IFormFile file, [FromForm] string? assetType)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Dữ liệu ảnh bảo hiểm không hợp lệ." });
            }

            if (file.Length > MaxInsuranceAssetBytes)
            {
                return BadRequest(new { message = "Ảnh bảo hiểm vượt quá dung lượng 10MB." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ tệp ảnh cho hồ sơ bảo hiểm." });
            }

            var normalizedAssetType = NormalizeInsuranceAssetType(assetType);
            if (normalizedAssetType == null)
            {
                return BadRequest(new { message = "Loại ảnh bảo hiểm không hợp lệ." });
            }

            try
            {
                var uploaded = await SaveInsuranceAssetFileAsync(file, normalizedAssetType);
                _context.ServiceOrderInsuranceUploads.Add(new ServiceOrderInsuranceUpload
                {
                    AssetType = normalizedAssetType,
                    BlobPath = uploaded.BlobPath,
                    FileUrl = uploaded.Url,
                    Status = ServiceOrderInsuranceUploadStatuses.Staged
                });
                await SaveChangesInTransactionAsync();

                return Ok(new
                {
                    url = uploaded.Url,
                    assetType = normalizedAssetType
                });
            }
            catch
            {
                return BadRequest(new { message = "Không thể tải ảnh bảo hiểm lên máy chủ." });
            }
        }

        private async Task<List<ServiceOrderResponseDto>> MapServiceOrderResponsesAsync(IEnumerable<ServiceOrder> items)
        {
            var serviceOrders = (items ?? Enumerable.Empty<ServiceOrder>()).ToList();
            if (serviceOrders.Count == 0)
            {
                return new List<ServiceOrderResponseDto>();
            }

            var orderIds = serviceOrders
                .Select(item => item.Id)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var assetLookup = await _context.ServiceOrderInsuranceAssets
                .AsNoTracking()
                .Where(asset => orderIds.Contains(asset.ServiceOrderId))
                .OrderBy(asset => asset.ServiceOrderId)
                .ThenBy(asset => asset.AssetType)
                .ThenBy(asset => asset.SortOrder)
                .ThenBy(asset => asset.Id)
                .ToListAsync();

            var assetsByOrderId = assetLookup
                .GroupBy(asset => asset.ServiceOrderId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var vehicleIds = serviceOrders
                .Select(item => item.VehicleId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var vehicleById = await _context.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicleIds.Contains(vehicle.Id))
                .Select(vehicle => new ServiceOrderVehicleSnapshot
                {
                    VehicleId = vehicle.Id,
                    LicensePlate = vehicle.LicensePlate,
                    ModelId = vehicle.ModelId,
                    ModelName = vehicle.Model != null ? vehicle.Model.ModelName : null,
                    BrandId = vehicle.Model != null ? (long?)vehicle.Model.BrandId : null,
                    BrandName = vehicle.Model != null && vehicle.Model.Brand != null
                        ? vehicle.Model.Brand.Name
                        : null
                })
                .ToDictionaryAsync(item => item.VehicleId);
            var insuranceCompanyByOrderId = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(detail =>
                    orderIds.Contains(detail.ServiceOrderId) &&
                    detail.InsuranceCompanyId.HasValue &&
                    detail.InsuranceCompanyId.Value > 0)
                .GroupBy(detail => detail.ServiceOrderId)
                .Select(group => new
                {
                    ServiceOrderId = group.Key,
                    InsuranceCompanyId = group
                        .OrderBy(detail => detail.Id)
                        .Select(detail => detail.InsuranceCompanyId)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(item => item.ServiceOrderId, item => item.InsuranceCompanyId);
            var discountByOrderId = await _context.ServiceOrderDiscounts
                .AsNoTracking()
                .Where(discount => orderIds.Contains(discount.ServiceOrderId))
                .GroupBy(discount => discount.ServiceOrderId)
                .Select(group => new
                {
                    ServiceOrderId = group.Key,
                    TotalDiscount = group.Sum(discount => discount.AppliedAmount > 0m ? discount.AppliedAmount : (discount.DiscountValue < 0m ? 0m : discount.DiscountValue))
                })
                .ToDictionaryAsync(item => item.ServiceOrderId, item => item.TotalDiscount);

            return serviceOrders
                .Select(item => MapServiceOrderResponse(
                    item,
                    vehicleById.TryGetValue(item.VehicleId, out var vehicle) ? vehicle : null,
                    assetsByOrderId.TryGetValue(item.Id, out var assets) ? assets : null,
                    insuranceCompanyByOrderId.TryGetValue(item.Id, out var insuranceCompanyId) ? insuranceCompanyId : null,
                    discountByOrderId.TryGetValue(item.Id, out var totalDiscount) ? totalDiscount : 0m))
                .ToList();
        }

        private async Task<ServiceOrderResponseDto> MapServiceOrderResponseAsync(ServiceOrder item)
        {
            var assets = await _context.ServiceOrderInsuranceAssets
                .AsNoTracking()
                .Where(asset => asset.ServiceOrderId == item.Id)
                .OrderBy(asset => asset.AssetType)
                .ThenBy(asset => asset.SortOrder)
                .ThenBy(asset => asset.Id)
                .ToListAsync();
            var insuranceCompanyId = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(detail =>
                    detail.ServiceOrderId == item.Id &&
                    detail.InsuranceCompanyId.HasValue &&
                    detail.InsuranceCompanyId.Value > 0)
                .OrderBy(detail => detail.Id)
                .Select(detail => detail.InsuranceCompanyId)
                .FirstOrDefaultAsync();
            var totalDiscount = await _context.ServiceOrderDiscounts
                .AsNoTracking()
                .Where(discount => discount.ServiceOrderId == item.Id)
                .SumAsync(discount => (decimal?)(discount.AppliedAmount > 0m ? discount.AppliedAmount : (discount.DiscountValue < 0m ? 0m : discount.DiscountValue))) ?? 0m;
            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.Id == item.VehicleId)
                .Select(v => new ServiceOrderVehicleSnapshot
                {
                    VehicleId = v.Id,
                    LicensePlate = v.LicensePlate,
                    ModelId = v.ModelId,
                    ModelName = v.Model != null ? v.Model.ModelName : null,
                    BrandId = v.Model != null ? (long?)v.Model.BrandId : null,
                    BrandName = v.Model != null && v.Model.Brand != null
                        ? v.Model.Brand.Name
                        : null
                })
                .FirstOrDefaultAsync();

            return MapServiceOrderResponse(item, vehicle, assets, insuranceCompanyId, totalDiscount);
        }

        private ServiceOrderResponseDto MapServiceOrderResponse(
            ServiceOrder item,
            ServiceOrderVehicleSnapshot? vehicle,
            IReadOnlyCollection<ServiceOrderInsuranceAsset>? assets,
            long? insuranceCompanyId,
            decimal totalDiscount)
        {
            var dto = _mapper.Map<ServiceOrderResponseDto>(item);
            dto.VehicleLicensePlate = vehicle?.LicensePlate;
            dto.VehicleModelId = vehicle?.ModelId;
            dto.VehicleModelName = vehicle?.ModelName;
            dto.VehicleBrandId = vehicle?.BrandId;
            dto.VehicleBrandName = vehicle?.BrandName;
            dto.InsuranceCompanyId = insuranceCompanyId > 0 ? insuranceCompanyId : null;
            dto.TotalDiscount = totalDiscount;
            dto.InsuranceCardImageUrls = assets?
                .Where(asset => string.Equals(asset.AssetType, AppointmentInsuranceAssetTypes.Card, StringComparison.OrdinalIgnoreCase))
                .Select(asset => asset.FileUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList() ?? new List<string>();
            dto.InsuranceDamageImageUrls = assets?
                .Where(asset => string.Equals(asset.AssetType, AppointmentInsuranceAssetTypes.Damage, StringComparison.OrdinalIgnoreCase))
                .Select(asset => asset.FileUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList() ?? new List<string>();
            return dto;
        }

        private sealed class ServiceOrderVehicleSnapshot
        {
            public long VehicleId { get; set; }
            public string? LicensePlate { get; set; }
            public long? BrandId { get; set; }
            public string? BrandName { get; set; }
            public long? ModelId { get; set; }
            public string? ModelName { get; set; }
        }

        private async Task<(string Url, string BlobPath)> SaveInsuranceAssetFileAsync(IFormFile file, string assetType)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var folderName = string.Equals(assetType, AppointmentInsuranceAssetTypes.Card, StringComparison.OrdinalIgnoreCase)
                ? "service-orders/insurance-cards"
                : "service-orders/insurance-damage";
            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var blobPath = $"{folderName.Trim('/')}/{fileName}";
            await using var stream = file.OpenReadStream();
            var url = await _cloudStorageService.UploadAsync(
                stream,
                folderName,
                fileName,
                file.ContentType,
                HttpContext.RequestAborted);
            return (url, blobPath);
        }

        private static string? NormalizeInsuranceAssetType(string? assetType)
        {
            var normalized = string.IsNullOrWhiteSpace(assetType)
                ? string.Empty
                : assetType.Trim().ToUpperInvariant();

            return normalized switch
            {
                AppointmentInsuranceAssetTypes.Card => AppointmentInsuranceAssetTypes.Card,
                AppointmentInsuranceAssetTypes.Damage => AppointmentInsuranceAssetTypes.Damage,
                _ => null
            };
        }

        private static string? NormalizeAndValidateFields(ServiceOrderCreateDto dto)
        {
            if (dto == null)
            {
                return "Dữ liệu Service Order không hợp lệ.";
            }

            var error = TryNormalizeOrderCategory(dto.OrderCategory, out var normalizedOrderCategory);
            if (error != null) return error;
            if (normalizedOrderCategory != null) dto.OrderCategory = normalizedOrderCategory;

            return null;
        }

        private static string? NormalizeAndValidateFields(ServiceOrderUpdateDto dto)
        {
            if (dto == null)
            {
                return "Dữ liệu Service Order không hợp lệ.";
            }

            var error = TryNormalizeOrderCategory(dto.OrderCategory, out var normalizedOrderCategory);
            if (error != null) return error;
            if (normalizedOrderCategory != null) dto.OrderCategory = normalizedOrderCategory;

            return null;
        }

        private static string? TryNormalizeChoice(
            string? rawValue,
            HashSet<string> allowedValues,
            string fieldName,
            out string? normalizedValue)
        {
            normalizedValue = null;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var token = NormalizeToken(rawValue);
            if (!allowedValues.Contains(token))
            {
                var allowed = string.Join(", ", allowedValues.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị {fieldName} không hợp lệ: {rawValue}. Chấp nhận: {allowed}.";
            }

            normalizedValue = token;
            return null;
        }

        private static string? TryNormalizeOrderCategory(string? rawValue, out string? normalizedValue)
        {
            normalizedValue = null;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var token = NormalizeToken(rawValue);
            if (!AllowedOrderCategories.Contains(token))
            {
                var allowed = string.Join(", ", AllowedOrderCategories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị orderCategory không hợp lệ: {rawValue}. Chấp nhận: {allowed}.";
            }

            normalizedValue = token;
            return null;
        }

        private static EditRequestPayload? DeserializeEditRequestPayload(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<EditRequestPayload>(raw, EditRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private async Task<(string RoleCode, string RoleName)> ResolveRoleInfoAsync(long staffId)
        {
            var claimRoleCode = this.GetCurrentRoleCode();
            var claimRoleName = this.GetCurrentRoleName();
            if (!string.IsNullOrWhiteSpace(claimRoleCode) || !string.IsNullOrWhiteSpace(claimRoleName))
            {
                return (claimRoleCode ?? string.Empty, claimRoleName ?? string.Empty);
            }

            var roleInfo = await (
                from staff in _context.Staffs.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on staff.RoleId equals role.Id into roleJoin
                from role in roleJoin.DefaultIfEmpty()
                where staff.Id == staffId
                select new
                {
                    role.RoleCode,
                    role.RoleName
                }).FirstOrDefaultAsync();

            return (roleInfo?.RoleCode ?? string.Empty, roleInfo?.RoleName ?? string.Empty);
        }

        private static bool CanApproveServiceOrderSection(string section, string roleCode, string roleName)
        {
            var normalizedSection = NormalizeSection(section, "WORKSHOP");
            var isAdmin = IsAdminRole(roleCode, roleName);
            var isManager = IsManagerRole(roleCode, roleName);
            var isWarehouse = IsWarehouseRole(roleCode, roleName);
            var isAccounting = IsAccountingRole(roleCode, roleName);

            return normalizedSection switch
            {
                "PARTS" => isWarehouse || isManager || isAdmin,
                "BILLING" => isAccounting || isAdmin,
                "FINANCE" => isAccounting || isAdmin,
                "CLOSED" => isAdmin,
                "GATE_PASS" => isAdmin,
                "QUOTE" => isManager || isAdmin,
                "ASSIGNMENT" => isManager || isAdmin,
                "WORKSHOP" => isManager || isAdmin,
                _ => isManager || isAdmin,
            };
        }

        private static bool IsAdminRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "ADMIN", "OWNER", "DIRECTOR", "CHU_XUONG", "SUPERADMIN", "SUPER_ADMIN");

        private static bool IsManagerRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC");

        private static bool IsWarehouseRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO");

        private static bool IsAccountingRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "ACCOUNTANT", "FINANCE", "KE_TOAN", "CHIEF_ACCOUNTANT", "CFO");

        private static bool ContainsAnyKeyword(string roleCode, string roleName, params string[] keywords)
        {
            var normalizedCode = NormalizeSearchToken(roleCode);
            var normalizedName = NormalizeSearchToken(roleName);

            foreach (var keyword in keywords)
            {
                var token = NormalizeSearchToken(keyword);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (normalizedCode.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                    normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeSearchToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .ToUpperInvariant()
                .Replace('-', '_')
                .Replace(' ', '_');
        }

        private static string NormalizeSection(string? section, string fallback)
        {
            var token = NormalizeToken(section);
            if (string.IsNullOrWhiteSpace(token))
            {
                token = NormalizeToken(fallback);
            }

            return token;
        }

        private static string NormalizeToken(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim()
                    .ToUpperInvariant()
                    .Replace("-", "_")
                    .Replace(" ", "_");
        }

        private sealed class EditRequestPayload
        {
            public string? Section { get; set; }
            public string? Reason { get; set; }
            public string? Status { get; set; }
            public long? RequestedByUserId { get; set; }
            public DateTime? RequestedAtUtc { get; set; }
            public long? DecidedByUserId { get; set; }
            public DateTime? DecidedAtUtc { get; set; }
            public string? DecisionNote { get; set; }
            public string? DecidedByRoleCode { get; set; }
            public string? DecidedByRoleName { get; set; }
            public DateTime? OverrideUntilUtc { get; set; }
        }

        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync();
        }

        private async Task SaveChangesInTransactionAsync()
        {
            await using var tx = await TryBeginTransactionAsync();
            await _context.SaveChangesAsync();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        }
    }
}
