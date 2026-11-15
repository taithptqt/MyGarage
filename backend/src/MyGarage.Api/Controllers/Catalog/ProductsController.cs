using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Support;

namespace MyGarage.Controllers
{
    [Route("api/v1/products")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IProductQueryService _queryService;

        public ProductsController(IProductService productService, IProductQueryService queryService)
        {
            _productService = productService;
            _queryService = queryService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedPageSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _queryService.GetPagedAsync(normalizedPage, normalizedPageSize, HttpContext.RequestAborted);
            var dtos = _productService.MapToDtos(paged);
            return Ok(PagedEnvelope<ProductResponseDto>.Create(dtos, total, normalizedPage, normalizedPageSize));
        }

        [AllowAnonymous]
        [HttpGet("retail-catalog")]
        public async Task<IActionResult> GetRetailCatalog(
            [FromQuery] string? search, [FromQuery] long? categoryId, [FromQuery] long? branchId,
            [FromQuery] string? sort, [FromQuery] int? page, [FromQuery] int? pageSize,
            CancellationToken cancellationToken)
        {
            if (branchId.HasValue && !await _queryService.BranchExistsAsync(branchId.Value, cancellationToken))
                return this.BadRequestProblem("Selected branch is invalid.", extensions: new Dictionary<string, object?> { ["branchId"] = branchId });

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize, 12, 100);
            var catalog = await _queryService.GetRetailCatalogAsync(
                search,
                categoryId,
                branchId,
                sort,
                normalizedPage,
                normalizedSize,
                cancellationToken);
            var publicItems = catalog.PageRows.Select(p => new { p.Id, p.Sku, p.Name, p.Unit, p.CategoryId,
                p.RetailPrice, p.TaxRate, p.WarrantyMonths, p.IsActive }).ToList();
            var envelope = PagedEnvelope<object>.Create(publicItems, catalog.Total, normalizedPage, normalizedSize);
            return Ok(new { items = publicItems,
                images = _productService.MapImagesToDtos(catalog.Images), catalog.Availability,
                total = envelope.Total, totalCount = envelope.TotalCount, activeCount = catalog.ActiveCount,
                page = envelope.Page, pageSize = envelope.PageSize,
                totalPages = envelope.TotalPages, hasPreviousPage = envelope.HasPreviousPage, hasNextPage = envelope.HasNextPage });
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<ProductResponseDto>> GetById(long id)
        {
            var dto = await _productService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [AllowAnonymous]
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
                return this.BadRequestProblem("ProductIds query is required.");
            }

            if (branchId.HasValue)
            {
                var branchExists = await _queryService.BranchExistsAsync(branchId.Value, cancellationToken);

                if (!branchExists)
                {
                    return this.BadRequestProblem("Selected branch is invalid.", extensions: new Dictionary<string, object?> { ["branchId"] = branchId });
                }
            }

            var availabilityEntries = await _queryService.GetStockAvailabilityAsync(
                normalizedProductIds,
                branchId,
                cancellationToken);

            return Ok(availabilityEntries);
        }

        [HttpGet("low-stock")]
        public async Task<ActionResult> GetLowStockProducts(
            [FromQuery] long? branchId = null,
            [FromQuery] int take = 200,
            CancellationToken cancellationToken = default)
        {
            if (take <= 0) take = 50;
            if (take > 1000) take = 1000;

            if (branchId.HasValue)
            {
                var branchExists = await _queryService.BranchExistsAsync(branchId.Value, cancellationToken);
                if (!branchExists)
                {
                    return this.BadRequestProblem("Selected branch is invalid.", extensions: new Dictionary<string, object?> { ["branchId"] = branchId });
                }
            }

            var data = await _queryService.GetLowStockProductsAsync(branchId, take, cancellationToken);

            return Ok(new
            {
                total = data.Count,
                items = data
            });
        }

        [AllowAnonymous]
        [HttpGet("{id:long}/retail-stock-by-branches")]
        public async Task<ActionResult<RetailProductBranchStockResponseDto>> GetRetailStockByBranches(
            long id,
            CancellationToken cancellationToken)
        {
            var productExists = await _queryService.ActiveProductExistsAsync(id, cancellationToken);

            if (!productExists)
            {
                return NotFound();
            }

            var rows = await _queryService.GetRetailStockBreakdownByProductAsync(
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
            var responseDto = await _productService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<ProductResponseDto>> Update(long id, [FromBody] ProductUpdateDto itemDto)
        {
            var responseDto = await _productService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

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
