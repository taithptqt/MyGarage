using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/deliveries")]
    [Route("api/v1/deliveries")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class DeliveryController : ControllerBase
    {
        private readonly IDeliveryService _deliveryService;
        private readonly IMapper _mapper;

        public DeliveryController(IDeliveryService deliveryService, IMapper mapper)
        {
            _deliveryService = deliveryService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<DeliveryResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _deliveryService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _mapper.Map<IEnumerable<DeliveryResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DeliveryResponseDto>> GetById(long id)
        {
            var item = await _deliveryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<DeliveryResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DeliveryResponseDto>> Create([FromBody] DeliveryCreateDto itemDto)
        {
            var entity = _mapper.Map<Delivery>(itemDto);
            var createdItem = await _deliveryService.AddAsync(entity);
            var responseDto = _mapper.Map<DeliveryResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DeliveryResponseDto>> Update(long id, [FromBody] DeliveryUpdateDto itemDto)
        {
            var existingItem = await _deliveryService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _deliveryService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<DeliveryResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _deliveryService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






