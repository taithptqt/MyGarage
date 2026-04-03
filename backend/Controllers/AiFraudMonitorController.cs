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
    [Route("api/ai-fraud-monitors")]
    [ApiController]
    public class AiFraudMonitorController : ControllerBase
    {
        private readonly IAiFraudMonitorService _aiFraudMonitorService;
        private readonly IMapper _mapper;

        public AiFraudMonitorController(IAiFraudMonitorService aiFraudMonitorService, IMapper mapper)
        {
            _aiFraudMonitorService = aiFraudMonitorService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AiFraudMonitorResponseDto>>> GetAll()
        {
            var items = await _aiFraudMonitorService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiFraudMonitorResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AiFraudMonitorResponseDto>> GetById(long id)
        {
            var item = await _aiFraudMonitorService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiFraudMonitorResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<AiFraudMonitorResponseDto>> Create([FromBody] AiFraudMonitorCreateDto itemDto)
        {
            var entity = _mapper.Map<AiFraudMonitor>(itemDto);
            var createdItem = await _aiFraudMonitorService.AddAsync(entity);
            var responseDto = _mapper.Map<AiFraudMonitorResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AiFraudMonitorResponseDto>> Update(long id, [FromBody] AiFraudMonitorUpdateDto itemDto)
        {
            var existingItem = await _aiFraudMonitorService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiFraudMonitorService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiFraudMonitorResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiFraudMonitorService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






