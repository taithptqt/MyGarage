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
using MyGarage.Common;
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

        private static readonly HashSet<string> AllowedStartWorkStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.APPROVED),
            nameof(ServiceOrderLifecycleStatus.WAITING_PARTS),
            nameof(ServiceOrderLifecycleStatus.IN_PROGRESS),
            nameof(ServiceOrderLifecycleStatus.REPAIRING),
        };

        private static readonly HashSet<string> ClosedOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.CLOSED),
            nameof(ServiceOrderLifecycleStatus.CANCELED),
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
                return NotFound(new { message = "Không tìm th?y service order." });
            }

            if (capability.CanEditWorkshop)
            {
                return BadRequest(new { message = "Service order hi?n chua b? khóa, không c?n g?i yêu c?u s?a." });
            }

            var reason = (dto?.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Vui lòng nh?p lý do yêu c?u s?a." });
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
            await using var transaction = await _context.Database.BeginTransactionAsync();
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

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
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
        }

        [Authorize]
        [HttpPost("{id:long}/start-work")]
        public async Task<IActionResult> StartWork(long id, [FromBody] ServiceOrderStartWorkRequestDto? dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var order = await _context.ServiceOrders
                .FirstOrDefaultAsync(x => x.Id == id);
            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            var normalizedStatus = NormalizeToken(order.Status);
            if (ClosedOrderStatuses.Contains(normalizedStatus))
            {
                return BadRequest(new { message = "Lệnh sửa chữa đã đóng, không thể bắt đầu thi công." });
            }

            if (!AllowedStartWorkStatuses.Contains(normalizedStatus))
            {
                return BadRequest(new { message = $"Trạng thái {order.Status} chưa thể bắt đầu thi công." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = VietnamTime.Now;

                if (!string.Equals(normalizedStatus, nameof(ServiceOrderLifecycleStatus.IN_PROGRESS), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalizedStatus, nameof(ServiceOrderLifecycleStatus.REPAIRING), StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = nameof(ServiceOrderLifecycleStatus.IN_PROGRESS);
                }

                order.UpdatedAt = now;
                order.UpdatedBy = actorUserId.Value;

                var partDetails = await _context.ServiceOrderDetails
                    .Where(x =>
                        x.ServiceOrderId == id &&
                        x.ItemId.HasValue &&
                        x.ItemId.Value > 0 &&
                        x.Quantity > 0m)
                    .ToListAsync();

                var normalizedPartDetails = partDetails
                    .Where(x => string.Equals(NormalizeServiceOrderDetailItemType(x.ItemType), "PART", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                long? materialRequestId = await _context.MaterialRequests
                    .Where(x => x.ServiceOrderId == id)
                    .OrderByDescending(x => x.Id)
                    .Select(x => (long?)x.Id)
                    .FirstOrDefaultAsync();

                var createdDetailCount = 0;
                if (normalizedPartDetails.Count > 0)
                {
                    if (!materialRequestId.HasValue || materialRequestId.Value <= 0)
                    {
                        var createdRequest = new MaterialRequest
                        {
                            ServiceOrderId = id,
                            TechnicianId = dto?.TechnicianId,
                            RequestDate = now,
                            Status = nameof(MaterialRequestStatusCode.PENDING),
                            Note = string.IsNullOrWhiteSpace(dto?.Note) ? "Auto create from start-work" : dto!.Note!.Trim(),
                            CreatedAt = now
                        };

                        _context.MaterialRequests.Add(createdRequest);
                        await _context.SaveChangesAsync();
                        materialRequestId = createdRequest.Id;
                    }

                    var existedDetailServiceOrderDetailIds = await (
                        from requestDetail in _context.MaterialRequestDetails.AsNoTracking()
                        join request in _context.MaterialRequests.AsNoTracking()
                            on requestDetail.RequestId equals request.Id
                        where request.ServiceOrderId == id &&
                              requestDetail.ServiceOrderDetailId.HasValue
                        select requestDetail.ServiceOrderDetailId!.Value)
                        .ToListAsync();
                    var existedSet = existedDetailServiceOrderDetailIds.ToHashSet();

                    var warehouseId = await _context.Warehouses
                        .AsNoTracking()
                        .Where(x => x.BranchId == order.ExecuteBranchId && x.IsDeleted != true && x.IsActive != false)
                        .OrderBy(x => x.Type == "MAIN" ? 0 : 1)
                        .ThenBy(x => x.Id)
                        .Select(x => (long?)x.Id)
                        .FirstOrDefaultAsync();

                    if (!warehouseId.HasValue)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Không có kho hoạt động tại chi nhánh thực hiện để tạo phiếu vật tư." });
                    }

                    foreach (var serviceOrderDetail in normalizedPartDetails)
                    {
                        if (existedSet.Contains(serviceOrderDetail.Id))
                        {
                            continue;
                        }

                        _context.MaterialRequestDetails.Add(new MaterialRequestDetail
                        {
                            RequestId = materialRequestId.Value,
                            ProductId = serviceOrderDetail.ItemId!.Value,
                            WarehouseId = warehouseId.Value,
                            RequestedQuantity = Math.Max(serviceOrderDetail.Quantity, 0m),
                            RequestedCondition = "NEW",
                            ServiceOrderDetailId = serviceOrderDetail.Id,
                            Status = nameof(MaterialRequestDetailStatusCode.PENDING),
                            Note = "Auto create from start-work",
                            CreatedAt = now,
                            CreatedBy = actorUserId.Value,
                            UpdatedAt = now,
                            UpdatedBy = actorUserId.Value
                        });

                        createdDetailCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    serviceOrderId = id,
                    status = order.Status,
                    materialRequestId,
                    createdMaterialRequestDetails = createdDetailCount,
                    message = "Đã bắt đầu thi công và đồng bộ yêu cầu vật tư."
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [Authorize]
        [HttpPost("{id:long}/cancel-with-rollback")]
        public async Task<IActionResult> CancelWithRollback(long id, [FromBody] ServiceOrderCancelWithRollbackRequestDto? dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var serviceOrder = await _context.ServiceOrders
                .FirstOrDefaultAsync(x => x.Id == id);
            if (serviceOrder == null)
            {
                return NotFound(new { message = "Không tìm thấy lệnh sửa chữa." });
            }

            if (string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.CANCELED), StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { message = "Lệnh đã ở trạng thái CANCELED.", serviceOrderId = id });
            }

            var hasCompletedPayment = await _context.Payments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ServiceOrderId == id &&
                    (x.Status ?? string.Empty).ToUpper() == nameof(PaymentStatusCode.COMPLETED));
            if (hasCompletedPayment)
            {
                return BadRequest(new { message = "Lệnh đã có thanh toán COMPLETED, không thể rollback tự động." });
            }

            var hasIssuedInvoice = await _context.Invoices
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ServiceOrderId == id &&
                    (x.IssuedAt.HasValue || (x.PaidAmount ?? 0m) > 0m));
            if (hasIssuedInvoice)
            {
                return BadRequest(new { message = "Lệnh đã phát sinh hóa đơn/thu tiền, không thể rollback tự động." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync();

                    var now = VietnamTime.Now;
                    var reason = string.IsNullOrWhiteSpace(dto?.Reason)
                        ? "Rollback one-click"
                        : dto!.Reason!.Trim();

                    var requestRows = await _context.MaterialRequests
                        .Where(x => x.ServiceOrderId == id)
                        .ToListAsync();
                    if (requestRows.Count > 0)
                    {
                        var requestIds = requestRows.Select(x => x.Id).ToList();
                        var requestDetailRows = await _context.MaterialRequestDetails
                            .Where(x => requestIds.Contains(x.RequestId))
                            .ToListAsync();
                        foreach (var detail in requestDetailRows)
                        {
                            detail.Status = nameof(MaterialRequestDetailStatusCode.CANCELED);
                        }

                        foreach (var row in requestRows)
                        {
                            row.Status = nameof(MaterialRequestStatusCode.CANCELED);
                            row.Note = string.IsNullOrWhiteSpace(row.Note)
                                ? $"Rollback: {reason}"
                                : $"{row.Note.Trim()} | Rollback: {reason}";
                        }
                    }

                    var qcRows = await _context.QcInspections
                        .Where(x => x.ServiceOrderId == id &&
                                    (x.Status ?? string.Empty).ToUpper() != nameof(QcInspectionStatusCode.PASS))
                        .ToListAsync();
                    foreach (var qc in qcRows)
                    {
                        qc.FailReason = string.IsNullOrWhiteSpace(qc.FailReason)
                            ? $"Rollback service order: {reason}"
                            : $"{qc.FailReason.Trim()} | Rollback service order: {reason}";
                    }

                    var linkRows = await (
                        from link in _context.QuotationAccessLinks
                        join quotation in _context.Quotations on link.QuotationId equals quotation.Id
                        where quotation.ServiceOrderId == id &&
                              (link.Status ?? string.Empty).ToUpper() == "ACTIVE"
                        select link
                    ).ToListAsync();
                    foreach (var link in linkRows)
                    {
                        link.Status = "REVOKED";
                        link.UpdatedAt = now;
                        link.UpdatedBy = actorUserId;
                    }

                    var allocations = await _context.BayAllocations
                        .Include(x => x.Bay)
                        .Where(x => x.ServiceOrderId == id && !x.ReleasedAt.HasValue)
                        .ToListAsync();
                    foreach (var allocation in allocations)
                    {
                        allocation.ReleasedAt = now;
                        allocation.ReleasedBy = actorUserId;
                        if (allocation.Bay != null)
                        {
                            allocation.Bay.OperationalStatus = "AVAILABLE";
                            allocation.Bay.CurrentOccupiedVehicleId = null;
                            allocation.Bay.LastStatusChangedAt = now;
                        }
                    }

                    serviceOrder.Status = nameof(ServiceOrderLifecycleStatus.CANCELED);
                    serviceOrder.CancellationReason = reason;
                    serviceOrder.InsuranceQuoteState = nameof(QuotationStatusCode.CANCELED);
                    serviceOrder.UpdatedAt = now;
                    serviceOrder.UpdatedBy = actorUserId.Value;

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                });

                return Ok(new
                {
                    serviceOrderId = id,
                    status = serviceOrder.Status,
                    message = "Đã rollback và hủy lệnh thành công."
                });
            }
            catch
            {
                throw;
            }
        }

        [Authorize]
        [HttpPost("{id:long}/warranty-rework")]
        public async Task<IActionResult> CreateWarrantyRework(long id, [FromBody] ServiceOrderWarrantyReworkCreateRequestDto? dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var original = await _context.ServiceOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (original == null)
            {
                return NotFound(new { message = "Khong tim thay service order goc." });
            }

            var hasQcFail = await _context.QcInspections
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ServiceOrderId == id &&
                    (x.Status ?? string.Empty).ToUpper() == nameof(QcInspectionStatusCode.FAIL));
            if (!hasQcFail)
            {
                return BadRequest(new { message = "Khong co QC FAIL hop le de mo luong bao hanh/rework." });
            }

            var reason = string.IsNullOrWhiteSpace(dto?.Reason)
                ? "Warranty rework from QC FAIL"
                : dto!.Reason!.Trim();

            var duplicateOpenRework = await _context.ServiceOrders
                .AsNoTracking()
                .AnyAsync(x =>
                    x.OriginalServiceOrderId == id &&
                    (x.OrderCategory ?? string.Empty).ToUpper() == "REWORK" &&
                    (x.Status ?? string.Empty).ToUpper() != nameof(ServiceOrderLifecycleStatus.CLOSED) &&
                    (x.Status ?? string.Empty).ToUpper() != nameof(ServiceOrderLifecycleStatus.CANCELED));
            if (duplicateOpenRework)
            {
                return BadRequest(new { message = "Da ton tai lenh REWORK dang mo cho service order goc nay." });
            }

            var now = VietnamTime.Now;
            var reworkOrder = new ServiceOrder
            {
                Code = $"RW-{original.Code}-{now:yyyyMMddHHmmss}",
                CustomerId = original.CustomerId,
                VehicleId = original.VehicleId,
                OriginBranchId = original.OriginBranchId,
                ExecuteBranchId = original.ExecuteBranchId,
                OriginalServiceOrderId = original.Id,
                OrderCategory = "REWORK",
                ServiceAdvisorId = original.ServiceAdvisorId,
                ReceptionistId = original.ReceptionistId,
                Status = nameof(ServiceOrderLifecycleStatus.NEW),
                CustomerNotes = reason,
                InsuranceQuoteState = null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorUserId.Value,
                UpdatedBy = actorUserId.Value
            };

            _context.ServiceOrders.Add(reworkOrder);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Da tao service order REWORK/WARRANTY.",
                originalServiceOrderId = original.Id,
                reworkServiceOrderId = reworkOrder.Id,
                reworkServiceOrderCode = reworkOrder.Code
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
                await _context.SaveChangesAsync();

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

            return serviceOrders
                .Select(item => MapServiceOrderResponse(
                    item,
                    assetsByOrderId.TryGetValue(item.Id, out var assets) ? assets : null,
                    insuranceCompanyByOrderId.TryGetValue(item.Id, out var insuranceCompanyId) ? insuranceCompanyId : null))
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

            return MapServiceOrderResponse(item, assets, insuranceCompanyId);
        }

        private ServiceOrderResponseDto MapServiceOrderResponse(
            ServiceOrder item,
            IReadOnlyCollection<ServiceOrderInsuranceAsset>? assets,
            long? insuranceCompanyId)
        {
            var dto = _mapper.Map<ServiceOrderResponseDto>(item);
            dto.CustomerNotes = CustomerNoteFormatter.ToDisplayText(dto.CustomerNotes);
            dto.InsuranceCompanyId = insuranceCompanyId > 0 ? insuranceCompanyId : null;
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

        private static string NormalizeServiceOrderDetailItemType(string? itemType)
        {
            var token = NormalizeToken(itemType);
            return token switch
            {
                "PART" or "PARTS" or "PRODUCT" or "MATERIAL" or "INVENTORY" => "PART",
                "SERVICE" or "STANDARD_SERVICE" or "LABOR" => "LABOR",
                _ => "LABOR"
            };
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
                return "Du lieu Service Order khong hop le.";
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
                return "Du lieu Service Order khong hop le.";
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
                return $"Gia tri {fieldName} khong hop le: {rawValue}. Chap nhan: {allowed}.";
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
                return $"Gia tri orderCategory khong hop le: {rawValue}. Chap nhan: {allowed}.";
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
                .Replace('d', 'd')
                .Replace('Ð', 'D')
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
    }
}
