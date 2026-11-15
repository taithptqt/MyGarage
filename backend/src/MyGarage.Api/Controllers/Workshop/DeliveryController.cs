using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/deliveries")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class DeliveryController : ControllerBase
    {
        private readonly IDeliveryService _deliveryService;

        public DeliveryController(IDeliveryService deliveryService)
        {
            _deliveryService = deliveryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<DeliveryResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _deliveryService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _deliveryService.MapToDtos(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DeliveryResponseDto>> GetById(long id)
        {
            var dto = await _deliveryService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DeliveryResponseDto>> Create([FromBody] DeliveryCreateDto itemDto)
        {
            var responseDto = await _deliveryService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DeliveryResponseDto>> Update(long id, [FromBody] DeliveryUpdateDto itemDto)
        {
            var responseDto = await _deliveryService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






