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
    public class VoucherController : ControllerBase
    {
        private readonly IVoucherService _voucherService;
        private readonly IMapper _mapper;

        public VoucherController(IVoucherService voucherService, IMapper mapper)
        {
            _voucherService = voucherService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherResponseDto>>> GetAll()
        {
            var items = await _voucherService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<VoucherResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> GetById(long id)
        {
            var item = await _voucherService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<VoucherResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VoucherResponseDto>> Create([FromBody] VoucherCreateDto itemDto)
        {
            var entity = _mapper.Map<Voucher>(itemDto);
            var createdItem = await _voucherService.AddAsync(entity);
            var responseDto = _mapper.Map<VoucherResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> Update(long id, [FromBody] VoucherUpdateDto itemDto)
        {
            var existingItem = await _voucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _voucherService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<VoucherResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _voucherService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





