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
    [Route("api/material-requests")]
[Route("api/[controller]")]
    [ApiController]
    public class MaterialRequestController : ControllerBase
    {
        private readonly IMaterialRequestService _materialRequestService;
        private readonly IMapper _mapper;

        public MaterialRequestController(IMaterialRequestService materialRequestService, IMapper mapper)
        {
            _materialRequestService = materialRequestService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaterialRequestResponseDto>>> GetAll()
        {
            var items = await _materialRequestService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<MaterialRequestResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MaterialRequestResponseDto>> GetById(long id)
        {
            var item = await _materialRequestService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<MaterialRequestResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialRequestResponseDto>> Create([FromBody] MaterialRequestCreateDto itemDto)
        {
            var entity = _mapper.Map<MaterialRequest>(itemDto);
            var createdItem = await _materialRequestService.AddAsync(entity);
            var responseDto = _mapper.Map<MaterialRequestResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MaterialRequestResponseDto>> Update(long id, [FromBody] MaterialRequestUpdateDto itemDto)
        {
            var existingItem = await _materialRequestService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _materialRequestService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<MaterialRequestResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _materialRequestService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






