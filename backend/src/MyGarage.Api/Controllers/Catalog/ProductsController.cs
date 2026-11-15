using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Controllers
{
    [Route("api/products")]
    [Route("api/v1/products")]
    [ApiController]
    [Authorize(Roles = "Staff")]
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

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedPageSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var query = _db.Products.AsNoTracking()
                .Where(x => x.IsDeleted != true).OrderByDescending(x => x.Id);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            var paged = offset >= total
                ? new List<Product>()
                : await query.Skip((int)offset).Take(normalizedPageSize).ToListAsync();
            var dtos = _mapper.Map<List<ProductResponseDto>>(paged);
            return Ok(PagedEnvelope<ProductResponseDto>.Create(dtos, total, normalizedPage, normalizedPageSize));
        }

        [AllowAnonymous]
        [HttpGet("retail-catalog")]
        public async Task<IActionResult> GetRetailCatalog(
            [FromQuery] string? search, [FromQuery] long? categoryId, [FromQuery] long? branchId,
            [FromQuery] string? sort, [FromQuery] int? page, [FromQuery] int? pageSize,
            CancellationToken cancellationToken)
        {
            if (branchId.HasValue && !await _db.Branches.AsNoTracking()
                .AnyAsync(b => b.Id == branchId && b.IsDeleted != true, cancellationToken))
                return BadRequest(new { message = "Selected branch is invalid.", branchId });

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize, 12, 100);
            var products = _db.Products.AsNoTracking().Where(p => p.IsDeleted != true && p.IsActive != false);
            var activeCount = await products.CountAsync(cancellationToken);
            if (categoryId.HasValue) products = products.Where(p => p.CategoryId == categoryId);

            var rows = products.Select(p => new
            {
                Product = p,
                Description = p.ProductImages.Where(i => i.Description != null && i.Description.Trim() != "")
                    .OrderByDescending(i => i.IsMain == true).ThenBy(i => i.SortOrder ?? 0).ThenBy(i => i.Id)
                    .Select(i => i.Description).FirstOrDefault(),
                CategoryName = p.Category != null && p.Category.IsDeleted != true ? p.Category.CategoryName : ""
            });
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                if (_db.Database.IsSqlServer())
                    rows = rows.Where(x => EF.Functions.Collate((x.Product.Name + " " + x.Product.Sku + " " +
                        (x.Description ?? "") + " " + x.CategoryName), "Latin1_General_100_CI_AI").Contains(term));
                else
                    rows = rows.Where(x => (x.Product.Name + " " + x.Product.Sku + " " +
                        (x.Description ?? "") + " " + x.CategoryName).ToLower().Contains(term));
            }
            var total = await rows.CountAsync(cancellationToken);
            var inventory = _db.Inventories.AsNoTracking().Where(i => i.Warehouse.IsDeleted != true &&
                i.Warehouse.IsActive != false && (!branchId.HasValue || i.Warehouse.BranchId == branchId));
            var withStock = rows.Select(x => new
            {
                x.Product,
                Quantity = inventory.Any(i => i.ProductId == x.Product.Id &&
                    ((i.Warehouse.Type ?? "").ToUpper().Contains("SELLABLE") || (i.Warehouse.Type ?? "").ToUpper().Contains("RETAIL")))
                    ? inventory.Where(i => i.ProductId == x.Product.Id &&
                        ((i.Warehouse.Type ?? "").ToUpper().Contains("SELLABLE") || (i.Warehouse.Type ?? "").ToUpper().Contains("RETAIL")))
                        .Sum(i => (decimal?)(i.Quantity - (i.ReservedQuantity ?? 0m))) ?? 0m
                    : inventory.Where(i => i.ProductId == x.Product.Id &&
                        !(i.Warehouse.Type ?? "").ToUpper().Contains("DEFECTIVE") && !(i.Warehouse.Type ?? "").ToUpper().Contains("TRANSIT"))
                        .Sum(i => (decimal?)(i.Quantity - (i.ReservedQuantity ?? 0m))) ?? 0m
            });
            var ordered = withStock.OrderByDescending(x => x.Quantity >= 1m);
            ordered = sort switch
            {
                "price-asc" => ordered.ThenBy(x => x.Product.RetailPrice ?? 0m),
                "price-desc" => ordered.ThenByDescending(x => x.Product.RetailPrice ?? 0m),
                "name-desc" => _db.Database.IsSqlServer()
                    ? ordered.ThenByDescending(x => EF.Functions.Collate(x.Product.Name, "Vietnamese_100_CI_AS"))
                    : ordered.ThenByDescending(x => x.Product.Name),
                _ => _db.Database.IsSqlServer()
                    ? ordered.ThenBy(x => EF.Functions.Collate(x.Product.Name, "Vietnamese_100_CI_AS"))
                    : ordered.ThenBy(x => x.Product.Name)
            };
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var pageRows = offset >= total ? new List<Product>() : await ordered.ThenBy(x => x.Product.Id)
                .Skip((int)offset).Take(normalizedSize).Select(x => x.Product).ToListAsync(cancellationToken);
            var ids = pageRows.Select(p => p.Id).ToList();
            var images = await _db.ProductImages.AsNoTracking().Where(i => ids.Contains(i.ProductId)).ToListAsync(cancellationToken);
            var availability = await RetailStockAvailabilityResolver.GetAvailableQuantitiesAsync(_db, ids, branchId, cancellationToken);
            var publicItems = pageRows.Select(p => new { p.Id, p.Sku, p.Name, p.Unit, p.CategoryId,
                p.RetailPrice, p.TaxRate, p.WarrantyMonths, p.IsActive }).ToList();
            var envelope = PagedEnvelope<object>.Create(publicItems, total, normalizedPage, normalizedSize);
            return Ok(new { items = publicItems,
                images = _mapper.Map<List<ProductImageResponseDto>>(images), availability,
                total = envelope.Total, totalCount = envelope.TotalCount, activeCount,
                page = envelope.Page, pageSize = envelope.PageSize,
                totalPages = envelope.TotalPages, hasPreviousPage = envelope.HasPreviousPage, hasNextPage = envelope.HasNextPage });
        }

        [AllowAnonymous]
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
                var branchExists = await _db.Branches
                    .AsNoTracking()
                    .AnyAsync(branch => branch.Id == branchId.Value && branch.IsDeleted != true, cancellationToken);
                if (!branchExists)
                {
                    return BadRequest(new { message = "Selected branch is invalid.", branchId });
                }
            }

            var rows = await _db.Inventories
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.Warehouse)
                    .ThenInclude(x => x.Branch)
                .Where(x =>
                    x.Product.IsDeleted != true &&
                    x.Product.IsActive != false &&
                    x.Warehouse.IsDeleted != true &&
                    x.Warehouse.IsActive != false &&
                    (!branchId.HasValue || x.Warehouse.BranchId == branchId.Value))
                .ToListAsync(cancellationToken);

            var data = rows
                .Select(x =>
                {
                    var minStock = Math.Max(0m, x.MinStockLevel ?? x.Product.MinStockLevel ?? 0m);
                    var reorderPoint = Math.Max(0m, x.Product.ReorderPoint ?? minStock);
                    var quantity = x.Quantity;
                    var status = quantity <= 0m
                        ? "OutOfStock"
                        : minStock > 0m && quantity <= minStock
                            ? "BelowMinStock"
                            : reorderPoint > 0m && quantity <= reorderPoint
                                ? "BelowReorder"
                                : string.Empty;

                    return new
                    {
                        productId = x.ProductId,
                        sku = x.Product.Sku,
                        productName = x.Product.Name,
                        warehouseId = x.WarehouseId,
                        warehouseName = x.Warehouse.Name,
                        branchId = x.Warehouse.BranchId,
                        branchName = x.Warehouse.Branch.Name,
                        quantity,
                        minStock,
                        reorderPoint,
                        status,
                        deficit = Math.Max(0m, minStock - quantity)
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.status))
                .OrderByDescending(x => x.status == "OutOfStock")
                .ThenByDescending(x => x.deficit)
                .ThenBy(x => x.productName)
                .Take(take)
                .ToList();

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
