using System;
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
    [Route("api/material-returns")]
    [ApiController]
    public class MaterialReturnController : ControllerBase
    {
        private readonly IMaterialReturnService _materialReturnService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;

        public MaterialReturnController(
            IMaterialReturnService materialReturnService,
            IServiceOrderService serviceOrderService,
            IMapper mapper)
        {
            _materialReturnService = materialReturnService;
            _serviceOrderService = serviceOrderService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaterialReturnResponseDto>>> GetAll()
        {
            var items = await _materialReturnService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<MaterialReturnResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MaterialReturnResponseDto>> GetById(long id)
        {
            var item = await _materialReturnService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<MaterialReturnResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialReturnResponseDto>> Create([FromBody] MaterialReturnCreateDto itemDto)
        {
            try
            {
                if (itemDto?.ServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(itemDto.ServiceOrderId.Value);
                }

                var entity = _mapper.Map<MaterialReturn>(itemDto);
                var createdItem = await _materialReturnService.AddAsync(entity);
                var responseDto = _mapper.Map<MaterialReturnResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MaterialReturnResponseDto>> Update(long id, [FromBody] MaterialReturnUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _materialReturnService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId;
                if (targetServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(targetServiceOrderId.Value);
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _materialReturnService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<MaterialReturnResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _materialReturnService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (existingItem.ServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(existingItem.ServiceOrderId.Value);
                }

                var result = await _materialReturnService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}






