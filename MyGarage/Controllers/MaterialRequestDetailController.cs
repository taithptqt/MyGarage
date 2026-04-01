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
    [Route("api/material-request-details")]
    [ApiController]
    public class MaterialRequestDetailController : ControllerBase
    {
        private readonly IMaterialRequestDetailService _materialRequestDetailService;
        private readonly IMapper _mapper;

        public MaterialRequestDetailController(IMaterialRequestDetailService materialRequestDetailService, IMapper mapper)
        {
            _materialRequestDetailService = materialRequestDetailService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaterialRequestDetailResponseDto>>> GetAll()
        {
            var items = await _materialRequestDetailService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<MaterialRequestDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> GetById(long id)
        {
            var item = await _materialRequestDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<MaterialRequestDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> Create([FromBody] MaterialRequestDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<MaterialRequestDetail>(itemDto);
            var createdItem = await _materialRequestDetailService.AddAsync(entity);
            var responseDto = _mapper.Map<MaterialRequestDetailResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> Update(long id, [FromBody] MaterialRequestDetailUpdateDto itemDto)
        {
            var existingItem = await _materialRequestDetailService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _materialRequestDetailService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<MaterialRequestDetailResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _materialRequestDetailService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






