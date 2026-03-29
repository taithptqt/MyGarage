using System.Collections.Generic;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RetailOrderController : ControllerBase
    {
        private readonly IRetailOrderService _retailOrderService;
        private readonly IMapper _mapper;
        private readonly GarageContext _context;

        public RetailOrderController(IRetailOrderService retailOrderService, IMapper mapper, GarageContext context)
        {
            _retailOrderService = retailOrderService;
            _mapper = mapper;
            _context = context;
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailOrderResponseDto>>> GetAll()
        {
            var items = await _retailOrderService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<RetailOrderResponseDto>> GetById(long id)
        {
            var item = await _retailOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                var customerId = GetUserIdFromToken();
                if (customerId == null || item.CustomerId != customerId.Value)
                {
                    return Forbid();
                }
            }

            var dto = _mapper.Map<RetailOrderResponseDto>(item);
            return Ok(dto);
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<object>>> GetMine()
        {
            var customerId = GetUserIdFromToken();
            if (customerId == null) return Unauthorized();

            var items = await _context.RetailOrders
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    id = x.Id,
                    orderCode = x.OrderCode,
                    status = x.Status,
                    subTotal = x.SubTotal,
                    finalAmount = x.FinalAmount,
                    createdAt = x.CreatedAt,
                    branchId = x.BranchId,
                    branchName = x.Branch != null ? x.Branch.Name : null
                })
                .ToListAsync();

            return Ok(items);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<RetailOrderResponseDto>> Create([FromBody] RetailOrderCreateDto itemDto)
        {
            var entity = _mapper.Map<RetailOrder>(itemDto);
            var createdItem = await _retailOrderService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailOrderResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("checkout")]
        public async Task<ActionResult<RetailOrderResponseDto>> Checkout([FromBody] RetailCheckoutRequestDto request)
        {
            if (request.BranchId <= 0)
            {
                return BadRequest(new { message = "Chi nhanh khong hop le." });
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { message = "Gio hang dang trong." });
            }

            if (request.Items.Any(x => x.Quantity <= 0))
            {
                return BadRequest(new { message = "So luong phai la so nguyen duong." });
            }

            var customerId = GetUserIdFromToken();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var customerExists = await _context.Customers
                .AnyAsync(c => c.Id == customerId.Value && c.IsDeleted != true && c.IsActive != false);
            if (!customerExists)
            {
                return Unauthorized();
            }

            var branchExists = await _context.Branches
                .AnyAsync(b => b.Id == request.BranchId && b.IsDeleted != true);
            if (!branchExists)
            {
                return BadRequest(new { message = "Chi nhanh khong ton tai." });
            }

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.IsDeleted != true && p.IsActive != false)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(new { message = "Co san pham khong ton tai hoac da ngung ban." });
            }

            var productPriceMap = products.ToDictionary(p => p.Id, p => p.RetailPrice ?? 0m);
            var normalizedItems = request.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            var subTotal = normalizedItems.Sum(i => productPriceMap[i.ProductId] * i.Quantity);
            var finalAmount = subTotal;
            var now = DateTime.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new RetailOrder
                {
                    BranchId = request.BranchId,
                    OrderCode = $"RO-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    CustomerId = customerId.Value,
                    GuestName = null,
                    GuestPhone = null,
                    SubTotal = subTotal,
                    TaxAmount = 0,
                    FinalAmount = finalAmount,
                    // RetailOrders status constraint does not accept "PENDING".
                    Status = "DRAFT",
                    CustomerNotes = string.IsNullOrWhiteSpace(request.CustomerNotes) ? null : request.CustomerNotes.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.RetailOrders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var line in normalizedItems)
                {
                    var unitPrice = productPriceMap[line.ProductId];
                    var lineSubTotal = unitPrice * line.Quantity;

                    _context.RetailOrderItems.Add(new RetailOrderItem
                    {
                        RetailOrderId = order.Id,
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        UnitPriceAtTime = unitPrice,
                        CostPriceAtTime = null,
                        SubTotal = lineSubTotal,
                        TaxRate = 0,
                        TaxAmount = 0,
                        FinalAmount = lineSubTotal
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                var dto = _mapper.Map<RetailOrderResponseDto>(order);
                return CreatedAtAction(nameof(GetById), new { id = order.Id }, dto);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<RetailOrderResponseDto>> Update(long id, [FromBody] RetailOrderUpdateDto itemDto)
        {
            var existingItem = await _retailOrderService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _retailOrderService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailOrderResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _retailOrderService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        private long? GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && long.TryParse(claim.Value, out var id))
                return id;
            return null;
        }
    }
}





