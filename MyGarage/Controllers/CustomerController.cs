using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IMapper _mapper;

        public CustomerController(ICustomerService customerService, IMapper mapper)
        {
            _customerService = customerService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerResponseDto>>> GetAll()
        {
            var customers = await _customerService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerResponseDto>> GetById(long id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CustomerResponseDto>(customer);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerResponseDto>> Create([FromBody] CustomerCreateDto customerDto)
        {
            var entity = _mapper.Map<Customer>(customerDto);
            var createdCustomer = await _customerService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerResponseDto>(createdCustomer);
            return CreatedAtAction(nameof(GetById), new { id = createdCustomer.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerResponseDto>> Update(long id, [FromBody] CustomerUpdateDto customerDto)
        {
            var existingCustomer = await _customerService.GetByIdAsync(id);
            if (existingCustomer == null) return NotFound();

            _mapper.Map(customerDto, existingCustomer);
            var updatedCustomer = await _customerService.UpdateAsync(id, existingCustomer);
            
            var responseDto = _mapper.Map<CustomerResponseDto>(updatedCustomer);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}


