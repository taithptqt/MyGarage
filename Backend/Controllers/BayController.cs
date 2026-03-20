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
    public class BayController : ControllerBase
    {
        private readonly IBayService _bayService;
        private readonly IMapper _mapper;

        public BayController(IBayService bayService, IMapper mapper)
        {
            _bayService = bayService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BayResponseDto>>> GetAll()
        {
            var items = await _bayService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<BayResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BayResponseDto>> GetById(long id)
        {
            var item = await _bayService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<BayResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BayResponseDto>> Create([FromBody] BayCreateDto itemDto)
        {
            var entity = _mapper.Map<Bay>(itemDto);
            var createdItem = await _bayService.AddAsync(entity);
            var responseDto = _mapper.Map<BayResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BayResponseDto>> Update(long id, [FromBody] BayUpdateDto itemDto)
        {
            var existingItem = await _bayService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _bayService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<BayResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _bayService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





