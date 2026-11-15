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
    [Route("api/v1/shifts")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ShiftController : ControllerBase
    {
        private readonly IShiftService _shiftService;

        public ShiftController(IShiftService shiftService)
        {
            _shiftService = shiftService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ShiftResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _shiftService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _shiftService.MapToDtos(paged);
            return Ok(PagedEnvelope<ShiftResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftResponseDto>> GetById(long id)
        {
            var dto = await _shiftService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ShiftResponseDto>> Create([FromBody] ShiftCreateDto itemDto)
        {
            var responseDto = await _shiftService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ShiftResponseDto>> Update(long id, [FromBody] ShiftUpdateDto itemDto)
        {
            var responseDto = await _shiftService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _shiftService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






