using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Controllers
{
    [Route("api/products")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public ProductsController(IProductService productService, IMapper mapper, IGarageContext db)
        {
            _productService = productService;
            _mapper = mapper;
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetAll()
        {
            var items = await _productService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ProductResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<ProductResponseDto>> GetById(long id)
        {
            var item = await _productService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<ProductResponseDto>(item);
            return Ok(dto);
        }

        [HttpGet("stock-availability")]
        public async Task<ActionResult<IEnumerable<RetailStockAvailabilityEntry>>> GetStockAvailability(
            [FromQuery] string? productIds,
            [FromQuery] long? branchId,
            CancellationToken cancellationToken)
        {
            var normalizedProductIds = (productIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(raw => long.TryParse(raw, out var value) ? value : 0L)
                .Where(value => value > 0)
                .Distinct()
                .ToList();

            if (normalizedProductIds.Count == 0)
            {
                return BadRequest(new { message = "ProductIds query is required." });
            }

            if (branchId.HasValue)
            {
                var branchExists = await _db.Branches
                    .AsNoTracking()
                    .AnyAsync(branch => branch.Id == branchId.Value && branch.IsDeleted != true, cancellationToken);

                if (!branchExists)
                {
                    return BadRequest(new { message = "Selected branch is invalid.", branchId });
                }
            }

            var availabilityEntries = await RetailStockAvailabilityResolver.GetAvailabilityEntriesAsync(
                _db,
                normalizedProductIds,
                branchId,
                cancellationToken);

            return Ok(availabilityEntries);
        }

        [HttpGet("{id:long}/retail-stock-by-branches")]
        public async Task<ActionResult<RetailProductBranchStockResponseDto>> GetRetailStockByBranches(
            long id,
            CancellationToken cancellationToken)
        {
            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(
                    product =>
                        product.Id == id &&
                        product.IsDeleted != true &&
                        product.IsActive != false,
                    cancellationToken);

            if (!productExists)
            {
                return NotFound();
            }

            var rows = await RetailStockAvailabilityResolver.GetRetailStockBreakdownByProductAsync(
                _db,
                id,
                cancellationToken);

            var dto = new RetailProductBranchStockResponseDto
            {
                ProductId = id,
                Branches = rows
                    .Select(row => new RetailBranchStockRowDto
                    {
                        BranchId = row.BranchId,
                        BranchName = row.BranchName,
                        AvailableQuantity = row.AvailableQuantity,
                    })
                    .ToList(),
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ProductResponseDto>> Create([FromBody] ProductCreateDto itemDto)
        {
            var entity = _mapper.Map<Product>(itemDto);
            var createdItem = await _productService.AddAsync(entity);
            var responseDto = _mapper.Map<ProductResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<ProductResponseDto>> Update(long id, [FromBody] ProductUpdateDto itemDto)
        {
            var existingItem = await _productService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _productService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<ProductResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _productService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
