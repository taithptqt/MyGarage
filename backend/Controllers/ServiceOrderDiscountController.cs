using System;
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
    [Route("api/service-order-discounts")]
    [ApiController]
    public class ServiceOrderDiscountController : ControllerBase
    {
        private readonly IServiceOrderDiscountService _serviceOrderDiscountService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;

        public ServiceOrderDiscountController(
            IServiceOrderDiscountService serviceOrderDiscountService,
            IServiceOrderService serviceOrderService,
            IMapper mapper)
        {
            _serviceOrderDiscountService = serviceOrderDiscountService;
            _serviceOrderService = serviceOrderService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDiscountResponseDto>>> GetAll()
        {
            var items = await _serviceOrderDiscountService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceOrderDiscountResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderDiscountService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceOrderDiscountResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDiscountResponseDto>> Create([FromBody] ServiceOrderDiscountCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return BadRequest(new { message = "Thiếu ServiceOrderId hợp lệ để tạo chiết khấu lệnh sửa chữa." });
                }

                await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(itemDto.ServiceOrderId);

                var entity = _mapper.Map<ServiceOrderDiscount>(itemDto);
                var createdItem = await _serviceOrderDiscountService.AddAsync(entity);
                var responseDto = _mapper.Map<ServiceOrderDiscountResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
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

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _serviceOrderDiscountService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<ServiceOrderDiscountResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}






