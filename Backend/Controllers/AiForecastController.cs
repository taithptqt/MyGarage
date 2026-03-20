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
    public class AiForecastController : ControllerBase
    {
        private readonly IAiForecastService _aiForecastService;
        private readonly IMapper _mapper;

        public AiForecastController(IAiForecastService aiForecastService, IMapper mapper)
        {
            _aiForecastService = aiForecastService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiForecastResponseDto>>> GetAll()
        {
            var items = await _aiForecastService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiForecastResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiForecastResponseDto>> GetById(long id)
        {
            var item = await _aiForecastService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiForecastResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiForecastResponseDto>> Create([FromBody] AiForecastCreateDto itemDto)
        {
            var entity = _mapper.Map<AiForecast>(itemDto);
            var createdItem = await _aiForecastService.AddAsync(entity);
            var responseDto = _mapper.Map<AiForecastResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiForecastResponseDto>> Update(long id, [FromBody] AiForecastUpdateDto itemDto)
        {
            var existingItem = await _aiForecastService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiForecastService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiForecastResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiForecastService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





