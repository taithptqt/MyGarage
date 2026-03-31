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
    [Route("api/suppliers")]
[Route("api/[controller]")]
    [ApiController]
    public class SupplierController : ControllerBase
    {
        private readonly ISupplierService _supplierService;
        private readonly IMapper _mapper;

        public SupplierController(ISupplierService supplierService, IMapper mapper)
        {
            _supplierService = supplierService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SupplierResponseDto>>> GetAll()
        {
            var items = await _supplierService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<SupplierResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SupplierResponseDto>> GetById(long id)
        {
            var item = await _supplierService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<SupplierResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<SupplierResponseDto>> Create([FromBody] SupplierCreateDto itemDto)
        {
            var entity = _mapper.Map<Supplier>(itemDto);
            var createdItem = await _supplierService.AddAsync(entity);
            var responseDto = _mapper.Map<SupplierResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SupplierResponseDto>> Update(long id, [FromBody] SupplierUpdateDto itemDto)
        {
            var existingItem = await _supplierService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _supplierService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<SupplierResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _supplierService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






