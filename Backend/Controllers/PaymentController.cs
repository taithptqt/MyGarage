using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Data;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IMapper _mapper;
        private readonly GarageContext _db;

        public PaymentController(IPaymentService paymentService, IMapper mapper, GarageContext db)
        {
            _paymentService = paymentService;
            _mapper = mapper;
            _db = db;
        }

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
            var dto = _mapper.Map<PaymentResponseDto>(item);
            return Ok(dto);
        }

        [HttpGet("{id}/status")]
        public async Task<ActionResult> GetStatus(long id)
        {
            var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null)
            {
                return NotFound();
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

