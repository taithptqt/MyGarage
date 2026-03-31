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
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use CustomerVehiclesController with route /api/customer-vehicles.")]
    public class CustomerVehicleController : ControllerBase
    {
        private readonly ICustomerVehicleService _customerVehicleService;
        private readonly IMapper _mapper;

        public CustomerVehicleController(ICustomerVehicleService customerVehicleService, IMapper mapper)
        {
            _customerVehicleService = customerVehicleService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerVehicleResponseDto>>> GetAll()
        {
            var items = await _customerVehicleService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CustomerVehicleResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerVehicleResponseDto>> GetById(long id)
        {
            var item = await _customerVehicleService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CustomerVehicleResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerVehicleResponseDto>> Create([FromBody] CustomerVehicleCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerVehicle>(itemDto);
            var createdItem = await _customerVehicleService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerVehicleResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerVehicleResponseDto>> Update(long id, [FromBody] CustomerVehicleUpdateDto itemDto)
        {
            var existingItem = await _customerVehicleService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _customerVehicleService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CustomerVehicleResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerVehicleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





