using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Common;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/retail-returns")]
    [Route("api/v1/retail-returns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnController : ControllerBase
    {
        private readonly IRetailReturnService _retailReturnService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public RetailReturnController(IRetailReturnService retailReturnService, IMapper mapper, IGarageContext context)
        {
            _retailReturnService = retailReturnService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<RetailReturnResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var query = _context.RetailReturns.AsNoTracking().AsQueryable();
            if (branchId.HasValue) query = query.Where(x => x.RetailOrder.BranchId == branchId.Value);
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<RetailReturn>()
                : await query.OrderByDescending(x => x.Id).Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = _mapper.Map<List<RetailReturnResponseDto>>(paged);
            return Ok(PagedEnvelope<RetailReturnResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var item = await _context.RetailReturns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id &&
                (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value));
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RetailReturnResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnResponseDto>> Create([FromBody] RetailReturnCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var actorId = this.GetCurrentUserId();
            if (!actorId.HasValue || actorId.Value <= 0) return Unauthorized();
            if (!await _context.RetailOrders.AsNoTracking().AnyAsync(x => x.Id == itemDto.RetailOrderId &&
                (!branchId.HasValue || x.BranchId == branchId.Value))) return NotFound();
            if (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Phiếu trả hàng mới phải ở trạng thái chờ xử lý." });
            var entity = _mapper.Map<RetailReturn>(itemDto);
            entity.Status = nameof(RetailReturnStatusCode.PENDING);
            entity.CreatedBy = actorId.Value;
            entity.UpdatedBy = null;
            entity.ApprovedBy = null;
            entity.ApprovedAt = null;
            entity.TotalRefundAmount = null;
            var createdItem = await _retailReturnService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailReturnResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> Update(long id, [FromBody] RetailReturnUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var existingItem = await _context.RetailReturns.Include(x => x.RetailOrder).FirstOrDefaultAsync(x => x.Id == id &&
                (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value));
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!string.Equals(existingItem.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Chỉ có thể chỉnh sửa phiếu trả hàng đang chờ xử lý." });
            if (itemDto.RetailOrderId != existingItem.RetailOrderId ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) && !string.Equals(itemDto.Status, existingItem.Status, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { message = "Không thể thay đổi đơn hoặc trạng thái qua chức năng chỉnh sửa." });
            existingItem.Reason = itemDto.Reason;
            existingItem.ReturnDate = itemDto.ReturnDate;
            existingItem.UpdatedBy = this.GetCurrentUserId();
            var updatedItem = await _retailReturnService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailReturnResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var existing = await _context.RetailReturns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id &&
                (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value));
            if (existing == null) return NotFound();
            if (!string.Equals(existing.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Chỉ có thể xóa phiếu trả hàng đang chờ xử lý." });
            var result = await _retailReturnService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id:long}/finalize")]
        public async Task<IActionResult> FinalizeReturn(long id)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0) return Unauthorized();
            if (!this.TryResolveBranchScope(null, out var scopedBranch, out var denied)) return denied!;
            if (_context.Database.CurrentTransaction != null)
                throw new InvalidOperationException("Không thể hoàn tất trả hàng trong giao dịch đang mở.");

            // Lock the order rather than just the return: several returns can reference
            // the same sold lines. Reload all state within each retry and transaction.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                _context.ChangeTracker.Clear();
                var orderId = await _context.RetailReturns.AsNoTracking()
                    .Where(x => x.Id == id && (!scopedBranch.HasValue || x.RetailOrder.BranchId == scopedBranch.Value))
                    .Select(x => (long?)x.RetailOrderId).FirstOrDefaultAsync();
                if (!orderId.HasValue) return NotFound(new { message = "Không tìm thấy phiếu trả hàng." });
                await using var tx = _context.Database.IsRelational()
                    ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                    : await TryBeginTransactionAsync();
                var committing = false;
                try
                {
                    if (_context.Database.IsSqlServer())
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"SELECT [Id] FROM [RetailOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId.Value}");
                    var retailReturn = await _context.RetailReturns
                        .Include(x => x.RetailReturnItems).ThenInclude(x => x.RetailOrderItem)
                        .Include(x => x.RetailOrder)
                        .FirstOrDefaultAsync(x => x.Id == id && x.RetailOrderId == orderId.Value &&
                            (!scopedBranch.HasValue || x.RetailOrder.BranchId == scopedBranch.Value));
                    if (retailReturn == null) return NotFound(new { message = "Không tìm thấy phiếu trả hàng." });
                    if (string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
                        return Ok(new { message = "Phiếu trả hàng đã hoàn tất.", retailReturnId = id });
                    if (!string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase))
                        return BadRequest(new { message = "Trạng thái phiếu trả hàng không hợp lệ để hoàn tất." });
                    if (retailReturn.RetailReturnItems.Count == 0)
                        return BadRequest(new { message = "Phiếu trả hàng chưa có dòng chi tiết." });

                    var warehouseIds = retailReturn.RetailReturnItems.Select(x => x.WarehouseId).Distinct().ToArray();
                    var validWarehouseIds = await _context.Warehouses.AsNoTracking()
                        .Where(x => warehouseIds.Contains(x.Id) && x.BranchId == retailReturn.RetailOrder.BranchId)
                        .Select(x => x.Id).ToListAsync();
                    if (validWarehouseIds.Count != warehouseIds.Length)
                        return BadRequest(new { message = "Kho trả hàng không thuộc chi nhánh của đơn bán lẻ." });
                    foreach (var group in retailReturn.RetailReturnItems.GroupBy(x => x.RetailOrderItemId))
                    {
                        var item = group.First().RetailOrderItem;
                        if (item == null || item.RetailOrderId != retailReturn.RetailOrderId)
                            return BadRequest(new { message = "Dòng trả hàng không thuộc đơn bán lẻ." });
                        if (group.Any(line => line.ReturnedQuantity <= 0m || line.RefundAmount < 0m))
                            return BadRequest(new { message = "Số lượng trả hoặc giá trị hoàn không hợp lệ." });
                        var alreadyReturnedQty = await _context.RetailReturnItems.AsNoTracking()
                            .Where(x => x.RetailOrderItemId == item.Id && x.RetailReturnId != retailReturn.Id &&
                                x.Status != nameof(RetailReturnStatusCode.REJECTED))
                            .SumAsync(x => (decimal?)x.ReturnedQuantity) ?? 0m;
                        if (alreadyReturnedQty + group.Sum(line => line.ReturnedQuantity) > item.Quantity + 0.0001m)
                            return BadRequest(new { message = $"Số lượng trả vượt quá số đã bán cho item #{item.Id}." });
                    }

                    var totalRefund = retailReturn.RetailReturnItems.Sum(line => line.RefundAmount);
                    if (totalRefund > retailReturn.RetailOrder.FinalAmount)
                        return BadRequest(new { message = "Giá trị hoàn vượt quá giá trị còn lại của đơn bán lẻ." });

                    var now = VietnamTime.Now;
                    foreach (var line in retailReturn.RetailReturnItems)
                    {
                        line.Status = nameof(RetailReturnStatusCode.COMPLETED);
                        line.UpdatedAt = now;
                        line.UpdatedBy = actorUserId.Value;
                        var hasStockTransaction = await _context.StockTransactions.AsNoTracking()
                            .AnyAsync(x => x.RetailReturnId == retailReturn.Id && x.ProductId == line.RetailOrderItem.ProductId && x.WarehouseId == line.WarehouseId);
                        if (!hasStockTransaction)
                            _context.StockTransactions.Add(new StockTransaction
                            {
                                ProductId = line.RetailOrderItem.ProductId, WarehouseId = line.WarehouseId,
                                Type = "IN", Quantity = line.ReturnedQuantity, Price = line.RefundAmount,
                                Note = $"Retail return #{retailReturn.ReturnCode}", RetailOrderId = retailReturn.RetailOrderId,
                                RetailReturnId = retailReturn.Id, CreatedAt = now, CreatedBy = actorUserId.Value
                            });
                    }
                    retailReturn.TotalRefundAmount = totalRefund;
                    retailReturn.Status = nameof(RetailReturnStatusCode.COMPLETED);
                    retailReturn.ApprovedBy = actorUserId.Value;
                    retailReturn.ApprovedAt = now;
                    retailReturn.UpdatedAt = now;
                    retailReturn.UpdatedBy = actorUserId.Value;
                    var order = retailReturn.RetailOrder;
                    order.FinalAmount = Math.Max(0m, order.FinalAmount - totalRefund);
                    order.UpdatedAt = now;
                    order.UpdatedBy = actorUserId.Value;
                    await SaveChangesInTransactionAsync();
                    if (tx != null) { committing = true; await tx.CommitAsync(); committing = false; }
                    return Ok(new { retailReturnId = retailReturn.Id, retailReturn.Status,
                        retailReturn.TotalRefundAmount, message = "Đã hoàn tất trả hàng." });
                }
                catch (Exception ex)
                {
                    if (tx != null) { try { await tx.RollbackAsync(); } catch { /* Preserve original error. */ } }
                    _context.ChangeTracker.Clear();
                    if (committing) throw new InvalidOperationException("Không xác định được kết quả trả hàng; kiểm tra phiếu trước khi thử lại.", ex);
                    throw;
                }
            });
        }
        private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> TryBeginTransactionAsync()
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



