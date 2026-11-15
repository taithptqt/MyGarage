using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Vehicles;

public sealed class EfVehicleHistoryStore : IVehicleHistoryStore
{
    private readonly IGarageContext _context;

    public EfVehicleHistoryStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<VehicleHistoryHeaderRecord?> GetVehicleAsync(long vehicleId)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .Where(x => x.Id == vehicleId && x.IsDeleted != true)
            .Select(x => new VehicleHistoryHeaderRecord(x.Id, x.LicensePlate, x.Vin))
            .FirstOrDefaultAsync();
    }

    public async Task<(int TotalCount, IReadOnlyList<VehicleHistoryOrderRecord> Orders)> GetPagedServiceOrdersAsync(
        long vehicleId,
        int page,
        int pageSize)
    {
        var baseQuery = _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.VehicleId == vehicleId);

        var total = await baseQuery.CountAsync();
        var orders = await baseQuery
            .OrderByDescending(x => x.CheckInTime ?? x.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VehicleHistoryOrderRecord(
                x.Id,
                x.Code,
                x.Status,
                x.CheckInTime,
                x.ActualHandover,
                x.FinalAmount,
                x.PaymentStatus,
                x.Qcstatus))
            .ToListAsync();

        return (total, orders);
    }

    public async Task<IReadOnlyList<VehicleHistoryDetailRecord>> GetOrderDetailsAsync(IReadOnlyCollection<long> orderIds)
    {
        if (orderIds.Count == 0)
        {
            return Array.Empty<VehicleHistoryDetailRecord>();
        }

        return await _context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.ServiceOrderId))
            .Select(x => new VehicleHistoryDetailRecord(x.ServiceOrderId, x.ItemType, x.ItemName))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<long, int>> GetDiagnosisCountsAsync(IReadOnlyCollection<long> orderIds)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        return await _context.Diagnoses
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.ServiceOrderId))
            .GroupBy(x => x.ServiceOrderId)
            .Select(x => new
            {
                ServiceOrderId = x.Key,
                Count = x.Count()
            })
            .ToDictionaryAsync(x => x.ServiceOrderId, x => x.Count);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetPaymentSumsAsync(IReadOnlyCollection<long> orderIds)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        return await _context.Payments
            .AsNoTracking()
            .Where(x => x.ServiceOrderId.HasValue && orderIds.Contains(x.ServiceOrderId.Value))
            .GroupBy(x => x.ServiceOrderId!.Value)
            .Select(x => new
            {
                ServiceOrderId = x.Key,
                PaidAmount = x.Sum(p => p.Amount)
            })
            .ToDictionaryAsync(x => x.ServiceOrderId, x => x.PaidAmount);
    }
}
