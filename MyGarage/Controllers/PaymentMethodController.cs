using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/payment-methods")]
[Route("api/[controller]")]
    [ApiController]
    public class PaymentMethodController : ControllerBase
    {
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IMapper _mapper;

        public PaymentMethodController(IPaymentMethodService paymentMethodService, IMapper mapper)
        {
            _paymentMethodService = paymentMethodService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentMethodResponseDto>>> GetAll()
        {
            var items = await _paymentMethodService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PaymentMethodResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentMethodResponseDto>> GetById(long id)
        {
            var item = await _paymentMethodService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<PaymentMethodResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentMethodResponseDto>> Create([FromBody] PaymentMethodCreateDto itemDto)
        {
            var entity = _mapper.Map<PaymentMethod>(itemDto);
            var createdItem = await _paymentMethodService.AddAsync(entity);
            var responseDto = _mapper.Map<PaymentMethodResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentMethodResponseDto>> Update(long id, [FromBody] PaymentMethodUpdateDto itemDto)
        {
            var existingItem = await _paymentMethodService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _paymentMethodService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<PaymentMethodResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _paymentMethodService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






