using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class ConsumableReturnService : IConsumableReturnService
    {
        private readonly IGarageContext _context;

        public ConsumableReturnService(IGarageContext context)
        {
            _context = context;
        }

        public async Task<ConsumableReturnBundleDto> CreateBundleAsync(ConsumableReturnCreateRequestDto request, long actorStaffId)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                throw new BadRequestException("Consumable return items are required.");
            }

            var serviceOrderExists = await _context.ServiceOrders.AsNoTracking()
                .AnyAsync(x => x.Id == request.ServiceOrderId);
            if (!serviceOrderExists)
            {
                throw new NotFoundException("Service order not found.");
            }

            var technicianExists = await _context.Staffs.AsNoTracking()
                .AnyAsync(x => x.Id == request.TechnicianId && x.IsDeleted != true);
            if (!technicianExists)
            {
                throw new NotFoundException("Technician not found.");
            }

            var now = VietnamTime.Now;
            var bundleCode = $"CRB-{now:yyyyMMddHHmmss}-{request.ServiceOrderId}";

            var bundle = new ConsumableReturnBundle
            {
                BundleCode = bundleCode,
                ServiceOrderId = request.ServiceOrderId,
                TechnicianId = request.TechnicianId,
                ReturnedAt = now,
                ReceivedBy = request.ReceivedBy,
                Status = "Confirmed",
                Notes = NormalizeText(request.Notes, 1000),
                CreatedAt = now
            };

            _context.ConsumableReturnBundles.Add(bundle);
            await _context.SaveChangesAsync();

            var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _context.Products.AsNoTracking()
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Unit, x.IsConsumable })
                .ToDictionaryAsync(x => x.Id);

            foreach (var item in request.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    throw new NotFoundException($"Product {item.ProductId} not found.");
                }

                if (product.IsConsumable != true)
                {
                    throw new BadRequestException($"Product {item.ProductId} is not marked as consumable.");
                }

                var issuedQty = Math.Max(0m, item.IssuedQuantity);
                var usedQty = Math.Max(0m, item.UsedQuantity);
                if (usedQty > issuedQty)
                {
                    throw new BadRequestException($"Used quantity cannot exceed issued quantity for product {item.ProductId}.");
                }

                var returnedQty = Math.Max(0m, issuedQty - usedQty);
                var unit = NormalizeText(item.Unit, 50) ?? product.Unit ?? "EA";

                var row = new ConsumableReturnBundleItem
                {
                    BundleId = bundle.Id,
                    ProductId = item.ProductId,
                    MaterialReqDetailId = item.MaterialReqDetailId,
                    IssuedQuantity = issuedQty,
                    UsedQuantity = usedQty,
                    Unit = unit
                };
                _context.ConsumableReturnBundleItems.Add(row);

                long? warehouseIdFromRequest = null;
                if (item.MaterialReqDetailId.HasValue && item.MaterialReqDetailId.Value > 0)
                {
                    var materialDetail = await _context.MaterialRequestDetails
                        .FirstOrDefaultAsync(x => x.Id == item.MaterialReqDetailId.Value);
                    if (materialDetail != null)
                    {
                        warehouseIdFromRequest = materialDetail.WarehouseId;
                        materialDetail.ReturnedQuantity = (materialDetail.ReturnedQuantity ?? 0m) + returnedQty;
                        materialDetail.UpdatedAt = now;
                        materialDetail.UpdatedBy = actorStaffId > 0 ? actorStaffId : (long?)null;
                    }
                }

                if (returnedQty > 0)
                {
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(x => x.ProductId == item.ProductId);
                    if (inventory != null)
                    {
                        inventory.Quantity += returnedQty;
                    }

                    _context.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = item.ProductId,
                        WarehouseId = inventory?.WarehouseId ?? warehouseIdFromRequest ?? throw new BadRequestException($"Warehouse is missing for product {item.ProductId}."),
                        Type = "ConsumableReturn",
                        Quantity = returnedQty,
                        Note = $"Bundle {bundleCode}",
                        CreatedAt = now,
                        CreatedBy = actorStaffId > 0 ? actorStaffId : (long?)null
                    });
                }
            }

            await _context.SaveChangesAsync();

            var createdItems = await _context.ConsumableReturnBundleItems.AsNoTracking()
                .Where(x => x.BundleId == bundle.Id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            return new ConsumableReturnBundleDto
            {
                Id = bundle.Id,
                BundleCode = bundle.BundleCode,
                ServiceOrderId = bundle.ServiceOrderId,
                TechnicianId = bundle.TechnicianId,
                ReceivedBy = bundle.ReceivedBy,
                Status = bundle.Status,
                Notes = bundle.Notes,
                ReturnedAt = bundle.ReturnedAt,
                Items = createdItems.Select(x => new ConsumableReturnBundleItemDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    MaterialReqDetailId = x.MaterialReqDetailId,
                    IssuedQuantity = x.IssuedQuantity,
                    UsedQuantity = x.UsedQuantity,
                    ReturnedQuantity = Math.Max(0m, x.IssuedQuantity - x.UsedQuantity),
                    Unit = x.Unit
                }).ToList()
            };
        }

        private static string? NormalizeText(string? value, int maxLength)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }
    }
}
