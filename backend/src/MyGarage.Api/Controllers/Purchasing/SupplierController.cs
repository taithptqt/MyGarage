using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/suppliers")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class SupplierController : ControllerBase
    {
        private readonly ISupplierService _supplierService;
        private readonly ISupplierQueryService _supplierQueryService;

        public SupplierController(ISupplierService supplierService, ISupplierQueryService supplierQueryService)
        {
            _supplierService = supplierService;
            _supplierQueryService = supplierQueryService;
        }

        [HttpGet]
        public async Task<ActionResult> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var hasPaging = page.HasValue || pageSize.HasValue;
            if (hasPaging)
            {
                var normalizedPage = page.GetValueOrDefault(1);
                if (normalizedPage < 1) normalizedPage = 1;

                var normalizedPageSize = pageSize.GetValueOrDefault(20);
                if (normalizedPageSize < 1) normalizedPageSize = 1;
                if (normalizedPageSize > 200) normalizedPageSize = 200;

                var (items, total) = await _supplierQueryService.GetPagedAsync(normalizedPage, normalizedPageSize);

                var pagedDtos = _supplierService.MapToDtos(items);
                return Ok(new
                {
                    items = pagedDtos,
                    total,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize)
                });
            }

            var dtos = await _supplierService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SupplierResponseDto>> GetById(long id)
        {
            var dto = await _supplierService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<SupplierResponseDto>> Create([FromBody] SupplierCreateDto itemDto)
        {
            var responseDto = await _supplierService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SupplierResponseDto>> Update(long id, [FromBody] SupplierUpdateDto itemDto)
        {
            var responseDto = await _supplierService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _supplierService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






