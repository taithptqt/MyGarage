using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public CategoriesController(ICategoryService categoryService, IMapper mapper, IGarageContext db)
        {
            _categoryService = categoryService;
            _mapper = mapper;
            _db = db;
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

                var query = _db.Categories
                    .AsNoTracking()
                    .Where(x => x.IsDeleted != true)
                    .OrderByDescending(x => x.Id);

                var total = await query.CountAsync();
                var items = await query
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToListAsync();

                var pagedDtos = _mapper.Map<List<CategoryResponseDto>>(items);
                return Ok(new
                {
                    items = pagedDtos,
                    total,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize)
                });
            }

            var items = await _categoryService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CategoryResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CategoryResponseDto>> GetById(long id)
        {
            var item = await _categoryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<CategoryResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CategoryCreateDto itemDto)
        {
            var entity = _mapper.Map<Category>(itemDto);
            var createdItem = await _categoryService.AddAsync(entity);
            var responseDto = _mapper.Map<CategoryResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CategoryResponseDto>> Update(long id, [FromBody] CategoryUpdateDto itemDto)
        {
            var existingItem = await _categoryService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _categoryService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<CategoryResponseDto>(updatedItem);
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
