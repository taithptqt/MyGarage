using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/v1/service-refunds")]
    [ApiController]
    public class ServiceRefundController : ControllerBase
    {
        private readonly IServiceRefundService _serviceRefundService;

        public ServiceRefundController(IServiceRefundService serviceRefundService)
        {
            _serviceRefundService = serviceRefundService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceRefundResponseDto>>> GetAll()
        {
            var dtos = await _serviceRefundService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceRefundResponseDto>> GetById(long id)
        {
            var dto = await _serviceRefundService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceRefundResponseDto>> Create([FromBody] ServiceRefundCreateDto itemDto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (actorUserId == null)
            {
                return Unauthorized();
            }

            var responseDto = await _serviceRefundService.CreateDtoAsync(itemDto, actorUserId.Value);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceRefundResponseDto>> Update(long id, [FromBody] ServiceRefundUpdateDto itemDto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (actorUserId == null)
            {
                return Unauthorized();
            }

            var responseDto = await _serviceRefundService.UpdateDtoAsync(id, itemDto, actorUserId.Value);
            if (responseDto == null)
            {
                return NotFound();
            }
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






