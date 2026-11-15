using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/suppliers")]
    [Route("api/v1/suppliers")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class SupplierController : ControllerBase
    {
        private readonly ISupplierService _supplierService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public SupplierController(ISupplierService supplierService, IMapper mapper, IGarageContext db)
        {
            _supplierService = supplierService;
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

                var query = _db.Suppliers
                    .AsNoTracking()
                    .Where(x => x.IsDeleted != true)
                    .OrderByDescending(x => x.Id);

                var total = await query.CountAsync();
                var items = await query
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToListAsync();

                var pagedDtos = _mapper.Map<List<SupplierResponseDto>>(items);
                return Ok(new
                {
                    items = pagedDtos,
                    total,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize)
                });
            }

            var allItems = await _supplierService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<SupplierResponseDto>>(allItems);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SupplierResponseDto>> GetById(long id)
        {
            var item = await _supplierService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<SupplierResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<SupplierResponseDto>> Create([FromBody] SupplierCreateDto itemDto)
        {
            var entity = _mapper.Map<Supplier>(itemDto);
            var createdItem = await _supplierService.AddAsync(entity);
            var responseDto = _mapper.Map<SupplierResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SupplierResponseDto>> Update(long id, [FromBody] SupplierUpdateDto itemDto)
        {
            var existingItem = await _supplierService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _supplierService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<SupplierResponseDto>(updatedItem);
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






