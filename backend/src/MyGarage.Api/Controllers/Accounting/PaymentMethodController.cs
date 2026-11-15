using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/payment-methods")]
    [ApiController]
    [Authorize]
    public class PaymentMethodController : ControllerBase
    {
        private readonly IPaymentMethodService _paymentMethodService;

        public PaymentMethodController(IPaymentMethodService paymentMethodService)
        {
            _paymentMethodService = paymentMethodService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentMethodResponseDto>>> GetAll()
        {
            var dtos = await _paymentMethodService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentMethodResponseDto>> GetById(long id)
        {
            var dto = await _paymentMethodService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpPost]
        public async Task<ActionResult<PaymentMethodResponseDto>> Create([FromBody] PaymentMethodCreateDto itemDto)
        {
            var responseDto = await _paymentMethodService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentMethodResponseDto>> Update(long id, [FromBody] PaymentMethodUpdateDto itemDto)
        {
            var responseDto = await _paymentMethodService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
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






