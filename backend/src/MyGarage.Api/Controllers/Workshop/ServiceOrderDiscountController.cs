using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/service-order-discounts")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ServiceOrderDiscountController : ControllerBase
    {
        private readonly IServiceOrderDiscountService _serviceOrderDiscountService;
        private readonly IServiceOrderService _serviceOrderService;

        public ServiceOrderDiscountController(
            IServiceOrderDiscountService serviceOrderDiscountService,
            IServiceOrderService serviceOrderService)
        {
            _serviceOrderDiscountService = serviceOrderDiscountService;
            _serviceOrderService = serviceOrderService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDiscountResponseDto>>> GetAll()
        {
            var dtos = await _serviceOrderDiscountService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> GetById(long id)
        {
            var dto = await _serviceOrderDiscountService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> Create([FromBody] ServiceOrderDiscountCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return this.BadRequestProblem("Thiếu ServiceOrderId hợp lệ để tạo chiết khấu lệnh sửa chữa.");
                }

                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(itemDto.ServiceOrderId);

                var responseDto = await _serviceOrderDiscountService.CreateDtoAsync(itemDto);
                return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> Update(long id, [FromBody] ServiceOrderDiscountUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _serviceOrderDiscountService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId > 0
                    ? itemDto.ServiceOrderId
                    : existingItem.ServiceOrderId;
                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(targetServiceOrderId);

                var responseDto = await _serviceOrderDiscountService.UpdateDtoAsync(id, existingItem, itemDto!);
                if (responseDto == null)
                {
                    return NotFound();
                }

                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _serviceOrderDiscountService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(existingItem.ServiceOrderId);

                var result = await _serviceOrderDiscountService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }
    }
}






