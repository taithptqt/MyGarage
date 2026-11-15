using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/categories")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ICategoryQueryService _categoryQueryService;

        public CategoriesController(ICategoryService categoryService, ICategoryQueryService categoryQueryService)
        {
            _categoryService = categoryService;
            _categoryQueryService = categoryQueryService;
        }

        [AllowAnonymous]
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

                var (items, total) = await _categoryQueryService.GetPagedAsync(normalizedPage, normalizedPageSize);

                var pagedDtos = _categoryService.MapToDtos(items);
                return Ok(new
                {
                    items = pagedDtos,
                    total,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize)
                });
            }

            var dtos = await _categoryService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<CategoryResponseDto>> GetById(long id)
        {
            var dto = await _categoryService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CategoryCreateDto itemDto)
        {
            var responseDto = await _categoryService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CategoryResponseDto>> Update(long id, [FromBody] CategoryUpdateDto itemDto)
        {
            var responseDto = await _categoryService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _categoryService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
