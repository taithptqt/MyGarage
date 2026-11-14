using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/material-requests")]
    [Route("api/v1/material-requests")]
    [ApiController]
    [Authorize]
    public class MaterialRequestController : ControllerBase
    {
        public sealed record MaterialRequestIssueEndpointDto(
            long? IssuedBy,
            long? ActorStaffId,
            string? Note,
            List<MaterialRequestIssuanceItemDto>? Items);

        public sealed record MaterialRequestRejectEndpointDto(
            string? Reason,
            string? Note,
            long? RejectedBy,
            long? ActorStaffId);

        private readonly IMaterialRequestService _materialRequestService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public MaterialRequestController(
            IMaterialRequestService materialRequestService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _materialRequestService = materialRequestService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var query = _context.MaterialRequests
                .AsNoTracking()
                .AsQueryable();

            if (scopedBranchId.HasValue)
            {
                query = query.Where(mr =>
                    (mr.ServiceOrder != null && mr.ServiceOrder.ExecuteBranchId == scopedBranchId.Value) ||
                    mr.MaterialRequestDetails.Any(d => d.Warehouse.BranchId == scopedBranchId.Value));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpperInvariant();
                query = query.Where(mr => mr.Status != null && mr.Status.ToUpper() == normalizedStatus);
            }

            var envelope = await query
                .Include(mr => mr.ServiceOrder)
                .Include(mr => mr.Technician)
                .Include(mr => mr.Storekeeper)
                .Include(mr => mr.MaterialRequestDetails)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(mr => mr.CreatedAt ?? mr.RequestDate)
                .ThenByDescending(mr => mr.Id)
                .ToPagedEnvelopeAsync<MaterialRequest, object>(page, pageSize, async pagedRequests =>
                {
                    var stockLookup = await LoadInventoryLookupAsync(pagedRequests);
                    return pagedRequests.Select(mr => MapMaterialRequestPayload(mr, stockLookup)).ToList();
                });
            return Ok(envelope);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var mr = await _context.MaterialRequests
                .AsNoTracking()
                .Include(x => x.ServiceOrder)
                .Include(x => x.Technician)
                .Include(x => x.Storekeeper)
                .Include(x => x.MaterialRequestDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (mr == null)
            {
                return NotFound();
            }

            if (!await BelongsToBranchScopeAsync(mr.Id, scopedBranchId))
            {
                return Forbid();
            }

            var stockLookup = await LoadInventoryLookupAsync(new[] { mr });
            return Ok(MapMaterialRequestPayload(mr, stockLookup));
        }

        [HttpPost]
        public async Task<ActionResult<MaterialRequestResponseDto>> Create([FromBody] MaterialRequestCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            try
            {
                if (itemDto?.ServiceOrderId > 0)
                {
                    if (scopedBranchId.HasValue)
                    {
                        var orderBranchId = await _context.ServiceOrders
                            .AsNoTracking()
                            .Where(so => so.Id == itemDto.ServiceOrderId.Value)
                            .Select(so => (long?)so.ExecuteBranchId)
                            .FirstOrDefaultAsync();
                        if (orderBranchId.HasValue && orderBranchId.Value != scopedBranchId.Value)
                        {
                            return Forbid();
                        }
                    }

                    await _serviceOrderService.EnsurePartsEditableAsync(itemDto.ServiceOrderId.Value);
                }

                var entity = _mapper.Map<MaterialRequest>(itemDto);
                entity.TechnicianId ??= this.GetCurrentUserId();
                var createdItem = await _materialRequestService.AddAsync(entity);
                var responseDto = _mapper.Map<MaterialRequestResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<MaterialRequestResponseDto>> Update(long id, [FromBody] MaterialRequestUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            try
            {
                var existingItem = await _materialRequestService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (!await BelongsToBranchScopeAsync(id, scopedBranchId))
                {
                    return Forbid();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId;
                if (targetServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(targetServiceOrderId.Value);
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _materialRequestService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<MaterialRequestResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:long}/issue")]
        public async Task<IActionResult> Issue(long id, [FromBody] MaterialRequestIssueEndpointDto? payload)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO", "FOREMAN", "TEAM_LEAD", "ADVISOR"))
            {
                return Forbid();
            }

            var mr = await _context.MaterialRequests
                .AsNoTracking()
                .Include(x => x.MaterialRequestDetails)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (mr == null)
            {
                return NotFound(new { message = $"Không tìm thấy phiếu yêu cầu materialRequestId: {id}" });
            }

            if (!await BelongsToBranchScopeAsync(id, scopedBranchId))
            {
                return Forbid();
            }

            var actorId = this.GetCurrentUserId() ?? payload?.ActorStaffId ?? payload?.IssuedBy ?? 0;
            if (actorId <= 0)
            {
                return BadRequest(new { message = "Không xác định được nhân viên thực hiện xuất kho." });
            }

            var items = payload?.Items?
                .Where(x => x != null && x.DetailId > 0 && x.QuantityToIssue > 0m)
                .ToList();

            if (items == null || items.Count == 0)
            {
                items = mr.MaterialRequestDetails
                    .Select(d => new MaterialRequestIssuanceItemDto
                    {
                        DetailId = d.Id,
                        QuantityToIssue = Math.Max(0m, d.RequestedQuantity - (d.IssuedQuantity ?? 0m))
                    })
                    .Where(x => x.QuantityToIssue > 0m)
                    .ToList();
            }

            if (items.Count == 0)
            {
                return BadRequest(new { message = "Phiếu yêu cầu không còn vật tư cần xuất." });
            }

            try
            {
                var updated = await _materialRequestService.IssuePartsAsync(new MaterialRequestIssuanceDto
                {
                    RequestId = id,
                    ActorStaffId = actorId,
                    Note = payload?.Note,
                    Items = items
                });

                return await GetById(updated.Id);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "Tồn kho vừa được cập nhật bởi giao dịch khác, vui lòng tải lại và thử lại." });
            }
        }

        [HttpPost("{id:long}/reject")]
        public async Task<IActionResult> Reject(long id, [FromBody] MaterialRequestRejectEndpointDto? payload)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO", "FOREMAN", "TEAM_LEAD", "ADVISOR"))
            {
                return Forbid();
            }

            var exists = await _context.MaterialRequests.AsNoTracking().AnyAsync(x => x.Id == id);
            if (!exists)
            {
                return NotFound(new { message = $"Không tìm thấy phiếu yêu cầu materialRequestId: {id}" });
            }

            if (!await BelongsToBranchScopeAsync(id, scopedBranchId))
            {
                return Forbid();
            }

            var actorId = this.GetCurrentUserId() ?? payload?.ActorStaffId ?? payload?.RejectedBy ?? 0;
            var reason = payload?.Reason ?? payload?.Note;

            try
            {
                var updated = await _materialRequestService.RejectRequestAsync(id, actorId, reason);
                return await GetById(updated.Id);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "Dữ liệu vừa được cập nhật bởi giao dịch khác, vui lòng tải lại và thử lại." });
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            try
            {
                var existingItem = await _materialRequestService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (!await BelongsToBranchScopeAsync(id, scopedBranchId))
                {
                    return Forbid();
                }

                if (existingItem.ServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(existingItem.ServiceOrderId.Value);
                }

                var result = await _materialRequestService.DeleteAsync(id);
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

        private async Task<bool> BelongsToBranchScopeAsync(long materialRequestId, long? scopedBranchId)
        {
            if (!scopedBranchId.HasValue)
            {
                return true;
            }

            return await _context.MaterialRequests
                .AsNoTracking()
                .AnyAsync(mr =>
                    mr.Id == materialRequestId &&
                    ((mr.ServiceOrder != null && mr.ServiceOrder.ExecuteBranchId == scopedBranchId.Value) ||
                     mr.MaterialRequestDetails.Any(d => d.Warehouse.BranchId == scopedBranchId.Value) ||
                     (mr.Technician != null && mr.Technician.BranchId == scopedBranchId.Value)));
        }

        private async Task<Dictionary<(long ProductId, long WarehouseId), Inventory>> LoadInventoryLookupAsync(
            IEnumerable<MaterialRequest> requests)
        {
            var pairs = requests
                .SelectMany(r => r.MaterialRequestDetails)
                .Select(d => new { d.ProductId, d.WarehouseId })
                .Distinct()
                .ToList();

            if (pairs.Count == 0)
            {
                return new Dictionary<(long ProductId, long WarehouseId), Inventory>();
            }

            var productIds = pairs.Select(p => p.ProductId).Distinct().ToList();
            var warehouseIds = pairs.Select(p => p.WarehouseId).Distinct().ToList();

            var inventories = await _context.Inventories
                .AsNoTracking()
                .Where(inv => productIds.Contains(inv.ProductId) && warehouseIds.Contains(inv.WarehouseId))
                .ToListAsync();

            return inventories.ToDictionary(inv => (inv.ProductId, inv.WarehouseId), inv => inv);
        }

        private static object MapMaterialRequestPayload(
            MaterialRequest mr,
            IReadOnlyDictionary<(long ProductId, long WarehouseId), Inventory> stockLookup)
        {
            return new
            {
                id = mr.Id,
                serviceOrderId = mr.ServiceOrderId,
                serviceOrderCode = mr.ServiceOrder?.Code,
                technicianId = mr.TechnicianId,
                requesterName = mr.Technician?.FullName,
                storekeeperId = mr.StorekeeperId,
                storekeeperName = mr.Storekeeper?.FullName,
                requestDate = mr.RequestDate,
                createdAt = mr.CreatedAt ?? mr.RequestDate,
                status = mr.Status,
                isRework = mr.IsRework,
                note = mr.Note,
                notes = mr.Note,
                items = mr.MaterialRequestDetails.Select(d =>
                {
                    stockLookup.TryGetValue((d.ProductId, d.WarehouseId), out var inv);
                    return new
                    {
                        id = d.Id,
                        productId = d.ProductId,
                        warehouseId = d.WarehouseId,
                        productName = d.Product?.Name,
                        productCode = d.Product?.Sku,
                        sku = d.Product?.Sku,
                        unit = d.Product?.Unit,
                        quantity = d.RequestedQuantity,
                        requestedQuantity = d.RequestedQuantity,
                        issuedQuantity = d.IssuedQuantity ?? 0m,
                        status = d.Status,
                        currentStock = inv?.Quantity ?? 0m,
                        unitCost = inv?.CurrentMacPrice ?? d.Product?.CostPrice ?? 0m
                    };
                }).ToList()
            };
        }
    }
}






