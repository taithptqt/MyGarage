using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/service-order-snapshots")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ServiceOrderSnapshotController : ControllerBase
    {
        private readonly IServiceOrderSnapshotService _serviceOrderSnapshotService;

        public ServiceOrderSnapshotController(IServiceOrderSnapshotService serviceOrderSnapshotService)
        {
            _serviceOrderSnapshotService = serviceOrderSnapshotService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderSnapshotResponseDto>>> GetAll()
        {
            var dtos = await _serviceOrderSnapshotService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderSnapshotResponseDto>> GetById(long id)
        {
            var dto = await _serviceOrderSnapshotService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderSnapshotResponseDto>> Create([FromBody] ServiceOrderSnapshotCreateDto itemDto)
        {
            var responseDto = await _serviceOrderSnapshotService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderSnapshotResponseDto>> Update(long id, [FromBody] ServiceOrderSnapshotUpdateDto itemDto)
        {
            var responseDto = await _serviceOrderSnapshotService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceOrderSnapshotService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






