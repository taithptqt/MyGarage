using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/customer-tiers")]
    [ApiController]
    [Authorize]
    public class CustomerTierController : ControllerBase
    {
        private readonly ICustomerTierService _customerTierService;

        public CustomerTierController(ICustomerTierService customerTierService)
        {
            _customerTierService = customerTierService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerTierResponseDto>>> GetAll()
        {
            var dtos = await _customerTierService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerTierResponseDto>> GetById(long id)
        {
            var dto = await _customerTierService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpPost]
        public async Task<ActionResult<CustomerTierResponseDto>> Create([FromBody] CustomerTierCreateDto itemDto)
        {
            var responseDto = await _customerTierService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerTierResponseDto>> Update(long id, [FromBody] CustomerTierUpdateDto itemDto)
        {
            var responseDto = await _customerTierService.UpdateDtoAsync(id, itemDto);
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
            var result = await _customerTierService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






