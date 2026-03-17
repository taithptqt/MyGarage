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
    public class GatePassController : ControllerBase
    {
        private readonly IGatePassService _gatePassService;
        private readonly IMapper _mapper;

        public GatePassController(IGatePassService gatePassService, IMapper mapper)
        {
            _gatePassService = gatePassService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<GatePassResponseDto>>> GetAll()
        {
            var items = await _gatePassService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<GatePassResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GatePassResponseDto>> GetById(long id)
        {
            var item = await _gatePassService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<GatePassResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<GatePassResponseDto>> Create([FromBody] GatePassCreateDto itemDto)
        {
            var entity = _mapper.Map<GatePass>(itemDto);
            var createdItem = await _gatePassService.AddAsync(entity);
            var responseDto = _mapper.Map<GatePassResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<GatePassResponseDto>> Update(long id, [FromBody] GatePassUpdateDto itemDto)
        {
            var existingItem = await _gatePassService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _gatePassService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<GatePassResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _gatePassService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





