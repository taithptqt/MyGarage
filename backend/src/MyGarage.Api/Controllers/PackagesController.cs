using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/packages")]
    [Route("api/v1/packages")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class PackagesController : ControllerBase
    {
        private readonly IPackageService _packageService;

        public PackagesController(IPackageService packageService)
        {
            _packageService = packageService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PackageResponseDto>>> GetAll()
        {
            var items = await _packageService.GetAllWithItemsAsync();
            return Ok(items.Select(ToResponseDto));
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<PackageResponseDto>> GetById(long id)
        {
            var item = await _packageService.GetByIdWithItemsAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(ToResponseDto(item));
        }

        [HttpPost]
        public async Task<ActionResult<PackageResponseDto>> Create([FromBody] PackageCreateDto dto)
        {
            var entity = ToEntity(dto);
            var created = await _packageService.AddAsync(entity);
            var loaded = await _packageService.GetByIdWithItemsAsync(created.Id) ?? created;
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToResponseDto(loaded));
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<PackageResponseDto>> Update(long id, [FromBody] PackageUpdateDto dto)
        {
            var existing = await _packageService.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Code = dto.Code.Trim();
            existing.Name = dto.Name.Trim();
            existing.CategoryId = dto.CategoryId;
            existing.BasePrice = dto.BasePrice;
            existing.StandardMinutes = dto.StandardMinutes;
            existing.Description = dto.Description;
            existing.ImageUrl = dto.ImageUrl;
            existing.IsActive = dto.IsActive ?? true;
            existing.UpdatedAt = DateTime.UtcNow;

            var updated = await _packageService.UpdateAsync(id, existing);
            var loaded = updated == null ? null : await _packageService.GetByIdWithItemsAsync(updated.Id);
            return loaded == null ? NotFound() : Ok(ToResponseDto(loaded));
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var deleted = await _packageService.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        private static Package ToEntity(PackageCreateDto dto)
        {
            var now = DateTime.UtcNow;
            return new Package
            {
                Code = dto.Code.Trim(),
                Name = dto.Name.Trim(),
                CategoryId = dto.CategoryId,
                BasePrice = dto.BasePrice,
                StandardMinutes = dto.StandardMinutes,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                IsActive = dto.IsActive ?? true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                PackageItems = dto.Items
                    .Where(item => item.StandardServiceId > 0)
                    .Select(item => new PackageItem
                    {
                        StandardServiceId = item.StandardServiceId,
                        Quantity = item.Quantity <= 0 ? 1m : item.Quantity,
                        SortOrder = item.SortOrder ?? 0,
                        IsDefault = item.IsDefault ?? true,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    })
                    .ToList()
            };
        }

        private static PackageResponseDto ToResponseDto(Package item)
        {
            return new PackageResponseDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                CategoryId = item.CategoryId,
                CategoryCode = item.Category?.Code,
                CategoryName = item.Category?.Name,
                BasePrice = item.BasePrice,
                StandardMinutes = item.StandardMinutes,
                Description = item.Description,
                ImageUrl = item.ImageUrl,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                Items = item.PackageItems
                    .OrderBy(packageItem => packageItem.SortOrder ?? 0)
                    .ThenBy(packageItem => packageItem.Id)
                    .Select(packageItem => new PackageItemResponseDto
                    {
                        Id = packageItem.Id,
                        PackageId = packageItem.PackageId,
                        StandardServiceId = packageItem.StandardServiceId,
                        StandardServiceCode = packageItem.StandardService?.Code,
                        StandardServiceName = packageItem.StandardService?.Name,
                        Quantity = packageItem.Quantity,
                        SortOrder = packageItem.SortOrder,
                        IsDefault = packageItem.IsDefault,
                        IsActive = packageItem.IsActive
                    })
                    .ToList()
            };
        }
    }
}
