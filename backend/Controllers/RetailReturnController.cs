using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
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

namespace MyGarage.Controllers
{
    [Route("api/retail-returns")]
    [ApiController]
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
        public async Task<ActionResult<IEnumerable<RetailReturnResponseDto>>> GetAll()
        {
            var items = await _retailReturnService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailReturnResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> GetById(long id)
        {
            var item = await _retailReturnService.GetByIdAsync(id);
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
            var entity = _mapper.Map<RetailReturn>(itemDto);
            var createdItem = await _retailReturnService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailReturnResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> Update(long id, [FromBody] RetailReturnUpdateDto itemDto)
        {
            var existingItem = await _retailReturnService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
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
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var retailReturn = await _context.RetailReturns
                .Include(x => x.RetailReturnItems)
                    .ThenInclude(x => x.RetailOrderItem)
                .Include(x => x.RetailOrder)
                    .ThenInclude(x => x.Invoices)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (retailReturn == null)
            {
                return NotFound(new { message = "Không tìm thấy phiếu trả hàng." });
            }

            if (string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { message = "Phiếu trả hàng đã hoàn tất.", retailReturnId = id });
            }

            if (retailReturn.RetailReturnItems.Count == 0)
            {
                return BadRequest(new { message = "Phiếu trả hàng chưa có dòng chi tiết." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                await using var tx = await TryBeginTransactionAsync();
                try
                {
                    var now = VietnamTime.Now;
                    decimal totalRefund = 0m;

                    foreach (var line in retailReturn.RetailReturnItems)
                    {
                        if (line.RetailOrderItem == null)
                        {
                            if (tx != null) await tx.RollbackAsync();
                            return BadRequest(new { message = $"Không tìm thấy retail order item cho dòng trả #{line.Id}." });
                        }
                        var retailOrderItem = line.RetailOrderItem;

                        if (line.ReturnedQuantity <= 0m)
                        {
                            if (tx != null) await tx.RollbackAsync();
                            return BadRequest(new { message = $"Dòng #{line.Id} có số lượng trả không hợp lệ." });
                        }

                        var soldQty = line.RetailOrderItem?.Quantity ?? 0m;
                        var alreadyReturnedQty = await _context.RetailReturnItems
                            .AsNoTracking()
                            .Where(x =>
                                x.RetailOrderItemId == line.RetailOrderItemId &&
                                x.RetailReturnId != retailReturn.Id &&
                                x.Status != nameof(RetailReturnStatusCode.REJECTED))
                            .SumAsync(x => (decimal?)x.ReturnedQuantity) ?? 0m;

                        if (alreadyReturnedQty + line.ReturnedQuantity > soldQty + 0.0001m)
                        {
                            if (tx != null) await tx.RollbackAsync();
                            return BadRequest(new
                            {
                                message = $"Số lượng trả vượt quá số đã bán cho item #{line.RetailOrderItemId}.",
                                soldQty,
                                alreadyReturnedQty,
                                currentReturnQty = line.ReturnedQuantity
                            });
                        }

                        line.Status = nameof(RetailReturnStatusCode.COMPLETED);
                        line.UpdatedAt = now;
                        line.UpdatedBy = actorUserId.Value;
                        totalRefund += line.RefundAmount;

                        var hasStockTransaction = await _context.StockTransactions
                            .AsNoTracking()
                            .AnyAsync(x => x.RetailReturnId == retailReturn.Id && x.ProductId == retailOrderItem.ProductId && x.WarehouseId == line.WarehouseId);
                        if (!hasStockTransaction)
                        {
                            _context.StockTransactions.Add(new StockTransaction
                            {
                                ProductId = retailOrderItem.ProductId,
                                WarehouseId = line.WarehouseId,
                                Type = "IN",
                                Quantity = line.ReturnedQuantity,
                                Price = line.RefundAmount,
                                Note = $"Retail return #{retailReturn.ReturnCode}",
                                RetailOrderId = retailReturn.RetailOrderId,
                                RetailReturnId = retailReturn.Id,
                                CreatedAt = now,
                                CreatedBy = actorUserId.Value
                            });
                        }
                    }

                    retailReturn.TotalRefundAmount = totalRefund;
                    retailReturn.Status = nameof(RetailReturnStatusCode.COMPLETED);
                    retailReturn.ApprovedBy = actorUserId.Value;
                    retailReturn.ApprovedAt = now;
                    retailReturn.UpdatedAt = now;
                    retailReturn.UpdatedBy = actorUserId.Value;

                    if (retailReturn.RetailOrder != null)
                    {
                        var order = retailReturn.RetailOrder;
                        var currentFinal = order.FinalAmount;
                        order.FinalAmount = Math.Max(0m, currentFinal - totalRefund);
                        order.UpdatedAt = now;
                        order.UpdatedBy = actorUserId.Value;

                        foreach (var invoice in order.Invoices)
                        {
                            var paid = invoice.PaidAmount ?? 0m;
                            var invoiceTotal = invoice.TotalAmount ?? 0m;
                            if (paid > invoiceTotal)
                            {
                                invoice.PaidAmount = invoiceTotal;
                            }

                            invoice.UpdatedAt = now;
                            invoice.UpdatedBy = actorUserId.Value;
                        }
                    }

                    await _context.SaveChangesAsync();
                    if (tx != null) await tx.CommitAsync();

                    return Ok(new
                    {
                        retailReturnId = retailReturn.Id,
                        retailReturn.Status,
                        retailReturn.TotalRefundAmount,
                        message = "Đã hoàn tất trả hàng và đồng bộ kho/doanh thu."
                    });
                }
                catch
                {
                    if (tx != null) await tx.RollbackAsync();
                    throw;
                }
            });
        }
        private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> TryBeginTransactionAsync()
        {
            try
            {
                return await _context.Database.BeginTransactionAsync();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}






