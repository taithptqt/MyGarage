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
    [Route("api/qc-inspections")]
    [ApiController]
    public class QcInspectionController : ControllerBase
    {
        private readonly IQcInspectionService _qcInspectionService;
        private readonly IMapper _mapper;

        public QcInspectionController(IQcInspectionService qcInspectionService, IMapper mapper)
        {
            _qcInspectionService = qcInspectionService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<QcInspectionResponseDto>>> GetAll()
        {
            var items = await _qcInspectionService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<QcInspectionResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<QcInspectionResponseDto>> GetById(long id)
        {
            var item = await _qcInspectionService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<QcInspectionResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<QcInspectionResponseDto>> Create([FromBody] QcInspectionCreateDto itemDto)
        {
            var entity = _mapper.Map<QcInspection>(itemDto);
            var createdItem = await _qcInspectionService.AddAsync(entity);
            var responseDto = _mapper.Map<QcInspectionResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<QcInspectionResponseDto>> Update(long id, [FromBody] QcInspectionUpdateDto itemDto)
        {
            var existingItem = await _qcInspectionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _qcInspectionService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<QcInspectionResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _qcInspectionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






