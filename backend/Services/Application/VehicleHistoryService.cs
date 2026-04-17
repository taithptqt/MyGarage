using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class VehicleHistoryService : IVehicleHistoryService
    {
        private readonly IGarageContext _context;

        public VehicleHistoryService(IGarageContext context)
        {
            _context = context;
        }

        public async Task<VehicleServiceHistoryResponseDto?> GetServiceHistoryAsync(long vehicleId, int page = 1, int pageSize = 20)
        {
            if (vehicleId <= 0)
            {
                return null;
            }

            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .Where(x => x.Id == vehicleId && x.IsDeleted != true)
                .Select(x => new
                {
                    x.Id,
                    x.LicensePlate,
                    x.Vin
                })
                .FirstOrDefaultAsync();
            if (vehicle == null)
            {
                return null;
            }

            var baseQuery = _context.ServiceOrders
                .AsNoTracking()
                .Where(x => x.VehicleId == vehicleId);

            var total = await baseQuery.CountAsync();
            var orders = await baseQuery
                .OrderByDescending(x => x.CheckInTime ?? x.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Status,
                    x.CheckInTime,
                    x.ActualHandover,
                    x.FinalAmount,
                    x.PaymentStatus,
                    x.Qcstatus
                })
                .ToListAsync();

            var orderIds = orders.Select(x => x.Id).ToList();
            var detailRows = await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => orderIds.Contains(x.ServiceOrderId))
                .Select(x => new DetailRow
                {
                    ServiceOrderId = x.ServiceOrderId,
                    ItemType = x.ItemType,
                    ItemName = x.ItemName
                })
                .ToListAsync();
            var diagnosisCounts = await _context.Diagnoses
                .AsNoTracking()
                .Where(x => orderIds.Contains(x.ServiceOrderId))
                .GroupBy(x => x.ServiceOrderId)
                .Select(x => new
                {
                    ServiceOrderId = x.Key,
                    Count = x.Count()
                })
                .ToDictionaryAsync(x => x.ServiceOrderId, x => x.Count);
            var paymentSums = await _context.Payments
                .AsNoTracking()
                .Where(x => x.ServiceOrderId.HasValue && orderIds.Contains(x.ServiceOrderId.Value))
                .GroupBy(x => x.ServiceOrderId!.Value)
                .Select(x => new
                {
                    ServiceOrderId = x.Key,
                    PaidAmount = x.Sum(p => p.Amount)
                })
                .ToDictionaryAsync(x => x.ServiceOrderId, x => x.PaidAmount);

            var detailsByOrder = detailRows
                .GroupBy(x => x.ServiceOrderId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var items = orders.Select(order =>
            {
                detailsByOrder.TryGetValue(order.Id, out var orderDetails);
                orderDetails ??= new List<DetailRow>();

                var serviceLineCount = orderDetails.Count(x => NormalizeType(x.ItemType) == "LABOR");
                var partLineCount = orderDetails.Count(x => NormalizeType(x.ItemType) == "PART");
                var highlights = orderDetails
                    .Select(x => x.ItemName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();

                return new VehicleServiceHistoryItemDto
                {
                    ServiceOrderId = order.Id,
                    ServiceOrderCode = order.Code ?? $"SO-{order.Id}",
                    Status = order.Status ?? string.Empty,
                    CheckInTime = order.CheckInTime,
                    ActualHandover = order.ActualHandover,
                    TotalAmount = order.FinalAmount ?? 0m,
                    PaidAmount = paymentSums.TryGetValue(order.Id, out var paid) ? paid : 0m,
                    PaymentStatus = order.PaymentStatus,
                    QcStatus = order.Qcstatus,
                    DiagnosisCount = diagnosisCounts.TryGetValue(order.Id, out var diagnosisCount) ? diagnosisCount : 0,
                    ServiceLineCount = serviceLineCount,
                    PartLineCount = partLineCount,
                    HighlightItems = highlights
                };
            }).ToList();

            return new VehicleServiceHistoryResponseDto
            {
                VehicleId = vehicle.Id,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                TotalServiceOrders = total,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize),
                Items = items
            };
        }

        private static string NormalizeType(string? value)
        {
            var token = (value ?? string.Empty).Trim().ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
            return token switch
            {
                "PART" or "PARTS" or "PRODUCT" or "MATERIAL" or "INVENTORY" => "PART",
                _ => "LABOR"
            };
        }

        private sealed class DetailRow
        {
            public long ServiceOrderId { get; set; }

            public string ItemType { get; set; } = string.Empty;

            public string ItemName { get; set; } = string.Empty;
        }
    }
}
