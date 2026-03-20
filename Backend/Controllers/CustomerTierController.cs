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
    public class CustomerTierController : ControllerBase
    {
        private readonly ICustomerTierService _customerTierService;
        private readonly IMapper _mapper;

        public CustomerTierController(ICustomerTierService customerTierService, IMapper mapper)
        {
            _customerTierService = customerTierService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerTierResponseDto>>> GetAll()
        {
            var items = await _customerTierService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CustomerTierResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerTierResponseDto>> GetById(long id)
        {
            var item = await _customerTierService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CustomerTierResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerTierResponseDto>> Create([FromBody] CustomerTierCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerTier>(itemDto);
            var createdItem = await _customerTierService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerTierResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerTierResponseDto>> Update(long id, [FromBody] CustomerTierUpdateDto itemDto)
        {
            var existingItem = await _customerTierService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _customerTierService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CustomerTierResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerTierService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





