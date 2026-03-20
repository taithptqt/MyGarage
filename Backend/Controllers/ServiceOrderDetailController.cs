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
    public class ServiceOrderDetailController : ControllerBase
    {
        private readonly IServiceOrderDetailService _serviceOrderDetailService;
        private readonly IMapper _mapper;

        public ServiceOrderDetailController(IServiceOrderDetailService serviceOrderDetailService, IMapper mapper)
        {
            _serviceOrderDetailService = serviceOrderDetailService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDetailResponseDto>>> GetAll()
        {
            var items = await _serviceOrderDetailService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceOrderDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ServiceOrderDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Create([FromBody] ServiceOrderDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceOrderDetail>(itemDto);
            var createdItem = await _serviceOrderDetailService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Update(long id, [FromBody] ServiceOrderDetailUpdateDto itemDto)
        {
            var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceOrderDetailService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceOrderDetailService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





