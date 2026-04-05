using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IMapper _mapper;

        public PaymentController(IPaymentService paymentService, IMapper mapper)
        {
            _paymentService = paymentService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetAll()
        {
            var items = await _paymentService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PaymentResponseDto>>(items);
            return Ok(dtos);
        }

        [Authorize]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<PaymentResponseDto>>> GetMine()
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var items = await _paymentService.GetByCustomerPayerAsync(customerId.Value);
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
            var dto = _mapper.Map<PaymentResponseDto>(item);
            return Ok(dto);
        }

        [Authorize]
        [HttpGet("{id:long}/status")]
        public async Task<ActionResult<object>> GetStatus(long id)
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var item = await _paymentService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound(new { message = "Payment not found." });
            }

            var isOwnedByCustomer =
                item.PayerId.HasValue &&
                item.PayerId.Value == customerId.Value &&
                string.Equals(item.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase);

            if (!isOwnedByCustomer)
            {
                return NotFound(new { message = "Payment not found." });
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(item.Status)
                ? "PENDING"
                : item.Status.Trim().ToUpperInvariant();

            return Ok(new
            {
                id = item.Id,
                status = normalizedStatus,
                amount = item.Amount,
                transactionCode = item.TransactionCode,
                updatedAt = item.UpdatedAt
            });
        }

        [HttpPost]
        public async Task<ActionResult<PaymentResponseDto>> Create([FromBody] PaymentCreateDto itemDto)
        {
            var entity = _mapper.Map<Payment>(itemDto);
            var createdItem = await _paymentService.AddAsync(entity);
            var responseDto = _mapper.Map<PaymentResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentResponseDto>> Update(long id, [FromBody] PaymentUpdateDto itemDto)
        {
            var existingItem = await _paymentService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _paymentService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<PaymentResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _paymentService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






