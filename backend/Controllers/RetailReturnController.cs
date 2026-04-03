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
    [Route("api/retail-returns")]
    [ApiController]
    public class RetailReturnController : ControllerBase
    {
        private readonly IRetailReturnService _retailReturnService;
        private readonly IMapper _mapper;

        public RetailReturnController(IRetailReturnService retailReturnService, IMapper mapper)
        {
            _retailReturnService = retailReturnService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RetailReturnResponseDto>>> GetAll()
        {
            var items = await _retailReturnService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<RetailReturnResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> GetById(long id)
        {
            var item = await _retailReturnService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<RetailReturnResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnResponseDto>> Create([FromBody] RetailReturnCreateDto itemDto)
        {
            var entity = _mapper.Map<RetailReturn>(itemDto);
            var createdItem = await _retailReturnService.AddAsync(entity);
            var responseDto = _mapper.Map<RetailReturnResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> Update(long id, [FromBody] RetailReturnUpdateDto itemDto)
        {
            var existingItem = await _retailReturnService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _retailReturnService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RetailReturnResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _retailReturnService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






