// Mục đích: Cung cấp API CRUD cho quan hệ gán voucher cho khách hàng và liên kết lịch sử sử dụng.
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
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerVoucherController : ControllerBase
    {
        private readonly ICustomerVoucherService _customerVoucherService;
        private readonly IMapper _mapper;

        public CustomerVoucherController(ICustomerVoucherService customerVoucherService, IMapper mapper)
        {
            _customerVoucherService = customerVoucherService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerVoucherResponseDto>>> GetAll()
        {
            var items = await _customerVoucherService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CustomerVoucherResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerVoucherResponseDto>> GetById(long id)
        {
            var item = await _customerVoucherService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CustomerVoucherResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerVoucherResponseDto>> Create([FromBody] CustomerVoucherCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerVoucher>(itemDto);
            var createdItem = await _customerVoucherService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerVoucherResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerVoucherResponseDto>> Update(long id, [FromBody] CustomerVoucherUpdateDto itemDto)
        {
            var existingItem = await _customerVoucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _customerVoucherService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CustomerVoucherResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerVoucherService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





