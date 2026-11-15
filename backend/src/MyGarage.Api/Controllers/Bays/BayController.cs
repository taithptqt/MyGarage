using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/bay")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class BayController : ControllerBase
    {
        private readonly IBayService _bayService;

        public BayController(IBayService bayService)
        {
            _bayService = bayService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BayResponseDto>>> GetAll()
        {
            var dtos = await _bayService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BayResponseDto>> GetById(long id)
        {
            var dto = await _bayService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BayResponseDto>> Create([FromBody] BayCreateDto itemDto)
        {
            var responseDto = await _bayService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BayResponseDto>> Update(long id, [FromBody] BayUpdateDto itemDto)
        {
            var responseDto = await _bayService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _bayService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





