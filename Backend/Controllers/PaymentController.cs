using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Data;
using MyGarage.Services;
using System;
using System.Linq;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IMapper _mapper;
        private readonly GarageContext _db;
        private readonly PaymentConfirmationService _paymentConfirmationService;

        public PaymentController(IPaymentService paymentService, IMapper mapper, GarageContext db, PaymentConfirmationService paymentConfirmationService)
        {
            _paymentService = paymentService;
            _mapper = mapper;
            _db = db;
            _paymentConfirmationService = paymentConfirmationService;
        }

        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetAll()
        {
            var items = await _paymentService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PaymentResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> GetById(long id)
        {
            var item = await _paymentService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!IsCurrentUserStaff() && !IsOwnedByCurrentUser(item))
            {
                return Forbid();
            }
            var dto = _mapper.Map<PaymentResponseDto>(item);
            return Ok(dto);
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetMine()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var items = await _db.Payments
                .AsNoTracking()
                .Where(p => p.PayerType == "CUSTOMER" && p.PayerId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            var dtos = _mapper.Map<IEnumerable<PaymentResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}/status")]
        public async Task<ActionResult> GetStatus(long id)
        {
            var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null)
            {
                return NotFound();
            }

            if (!IsCurrentUserStaff() && !IsOwnedByCurrentUser(payment))
            {
                return Forbid();
            }

            return Ok(new
            {
                payment.Id,
                payment.TransactionCode,
                payment.Amount,
                payment.Status,
                payment.Type,
                payment.CreatedAt,
                payment.UpdatedAt,
                IsCompleted = string.Equals(payment.Status, "COMPLETED", System.StringComparison.OrdinalIgnoreCase)
            });
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        public async Task<ActionResult<PaymentResponseDto>> Create([FromBody] PaymentCreateDto itemDto)
        {
            // Validate Source Reference
            var sourceCount = (itemDto.InvoiceId.HasValue ? 1 : 0) + 
                              (itemDto.ServiceOrderId.HasValue ? 1 : 0) + 
                              (itemDto.RetailOrderId.HasValue ? 1 : 0);
                              
            if (sourceCount > 1)
            {
                return BadRequest(new { message = "Payment must reference at most one target document (Invoice, ServiceOrder, or RetailOrder)." });
            }

            var entity = _mapper.Map<Payment>(itemDto);
            
            // Temporary trick: if the user passes COMPLETED, we set to PENDING first so that ApplyOutcomeAsync registers it as a transition.
            var intendedStatus = string.IsNullOrWhiteSpace(itemDto.Status) ? "PENDING" : itemDto.Status.Trim().ToUpperInvariant();
            entity.Status = "PENDING"; 

            var createdItem = await _paymentService.AddAsync(entity);
            
            if (intendedStatus == "COMPLETED")
            {
                // Sync with invoice / order
                await _paymentConfirmationService.ApplyOutcomeAsync(createdItem, createdItem.Amount, "COMPLETED");
            }

            var responseDto = _mapper.Map<PaymentResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> Update(long id, [FromBody] PaymentUpdateDto itemDto)
        {
            var existingItem = await _paymentService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var currentStatus = (existingItem.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (currentStatus == "COMPLETED")
            {
                return BadRequest(new { message = "Cannot modify a COMPLETED payment. Strict accounting policy requires issuing a logical refund or reversal instead." });
            }

            // Apply updates to fields (Amount, Type, etc.)
            _mapper.Map(itemDto, existingItem);
            
            var targetStatus = string.IsNullOrWhiteSpace(itemDto.Status) ? "PENDING" : itemDto.Status.Trim().ToUpperInvariant();

            // Explicitly sync if transforming from non-COMPLETED into COMPLETED
            if (targetStatus == "COMPLETED")
            {
                // ApplyOutcomeAsync handles the DB save and related document updates internally.
                await _paymentConfirmationService.ApplyOutcomeAsync(existingItem, itemDto.Amount ?? existingItem.Amount, "COMPLETED");
            }
            else
            {
                // Just normal update
                existingItem.Status = targetStatus;
                await _paymentService.UpdateAsync(id, existingItem);
            }
            
            var responseDto = _mapper.Map<PaymentResponseDto>(existingItem);
            return Ok(responseDto);
        }

        [Authorize(Roles = "Staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var existingItem = await _paymentService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var currentStatus = (existingItem.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (currentStatus == "COMPLETED" || currentStatus == "REFUNDED" || currentStatus == "REVERSED")
            {
                return BadRequest(new { message = "Strict internal policy prohibits deleting a processed financial transaction." });
            }

            var result = await _paymentService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        private long? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;
            return long.TryParse(claim.Value, out var id) ? id : null;
        }

        private bool IsCurrentUserStaff()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return string.Equals(role, "Staff", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOwnedByCurrentUser(Payment payment)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return false;
            return string.Equals(payment.PayerType, "CUSTOMER", System.StringComparison.OrdinalIgnoreCase)
                && payment.PayerId == userId.Value;
        }
    }
}

