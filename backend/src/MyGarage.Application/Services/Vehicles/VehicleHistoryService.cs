using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class VehicleHistoryService : IVehicleHistoryService
    {
        private readonly IVehicleHistoryStore _store;

        public VehicleHistoryService(IVehicleHistoryStore store)
        {
            _store = store;
        }

        public async Task<VehicleServiceHistoryResponseDto?> GetServiceHistoryAsync(long vehicleId, int page = 1, int pageSize = 20)
        {
            if (vehicleId <= 0)
            {
                return null;
            }

            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

            var vehicle = await _store.GetVehicleAsync(vehicleId);
            if (vehicle == null)
            {
                return null;
            }

            var (total, orders) = await _store.GetPagedServiceOrdersAsync(vehicleId, normalizedPage, normalizedPageSize);

            var orderIds = orders.Select(x => x.Id).ToList();
            var detailRows = await _store.GetOrderDetailsAsync(orderIds);
            var diagnosisCounts = await _store.GetDiagnosisCountsAsync(orderIds);
            var paymentSums = await _store.GetPaymentSumsAsync(orderIds);

            var detailsByOrder = detailRows
                .GroupBy(x => x.ServiceOrderId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var items = orders.Select(order =>
            {
                detailsByOrder.TryGetValue(order.Id, out var orderDetails);
                orderDetails ??= new List<VehicleHistoryDetailRecord>();

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
                    QcStatus = order.QcStatus,
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
    }
}
