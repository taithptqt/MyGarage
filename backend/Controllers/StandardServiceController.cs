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
    public class StandardServiceController : ControllerBase
    {
        private readonly IStandardServiceService _standardServiceService;
        private readonly IMapper _mapper;

        public StandardServiceController(IStandardServiceService standardServiceService, IMapper mapper)
        {
            _standardServiceService = standardServiceService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StandardServiceResponseDto>>> GetAll()
        {
            var items = await _standardServiceService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<StandardServiceResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StandardServiceResponseDto>> GetById(long id)
        {
            var item = await _standardServiceService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<StandardServiceResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StandardServiceResponseDto>> Create([FromBody] StandardServiceCreateDto itemDto)
        {
            var entity = _mapper.Map<StandardService>(itemDto);
            var createdItem = await _standardServiceService.AddAsync(entity);
            var responseDto = _mapper.Map<StandardServiceResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StandardServiceResponseDto>> Update(long id, [FromBody] StandardServiceUpdateDto itemDto)
        {
            var existingItem = await _standardServiceService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _standardServiceService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<StandardServiceResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _standardServiceService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





