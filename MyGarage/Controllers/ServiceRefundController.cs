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
    public class ServiceRefundController : ControllerBase
    {
        private readonly IServiceRefundService _serviceRefundService;
        private readonly IMapper _mapper;

        public ServiceRefundController(IServiceRefundService serviceRefundService, IMapper mapper)
        {
            _serviceRefundService = serviceRefundService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceRefundResponseDto>>> GetAll()
        {
            var items = await _serviceRefundService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceRefundResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceRefundResponseDto>> GetById(long id)
        {
            var item = await _serviceRefundService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceRefundResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceRefundResponseDto>> Create([FromBody] ServiceRefundCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceRefund>(itemDto);
            var createdItem = await _serviceRefundService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceRefundResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceRefundResponseDto>> Update(long id, [FromBody] ServiceRefundUpdateDto itemDto)
        {
            var existingItem = await _serviceRefundService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceRefundService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ServiceRefundResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceRefundService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





