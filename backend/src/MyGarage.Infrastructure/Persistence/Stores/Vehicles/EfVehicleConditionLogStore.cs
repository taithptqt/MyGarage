using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Vehicles;

public sealed class EfVehicleConditionLogStore : IVehicleConditionLogStore
{
    private const string StatusPending = nameof(DiagnosisStatus.PENDING);
    private const string StatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
    private const string LogTypeCheckIn = "CHECK_IN";

    private readonly IGarageContext _context;

    public EfVehicleConditionLogStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> ServiceOrderExistsAsync(long serviceOrderId)
    {
        return await _context.ServiceOrders.AnyAsync(x => x.Id == serviceOrderId);
    }

    public async Task<bool> TechnicianExistsAsync(long technicianId)
    {
        return await _context.Staffs.AnyAsync(x => x.Id == technicianId);
    }

    public async Task<IReadOnlyList<VehicleConditionLog>> GetByOrderAsync(long serviceOrderId)
    {
        return await _context.VehicleConditionLogs
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VehicleConditionLog>> GetActiveRequestsAsync(string normalizedLogType)
    {
        if (normalizedLogType == LogTypeCheckIn)
        {
            return await _context.VehicleConditionLogs
                .Where(x => x.LogType == normalizedLogType)
                .Where(x => x.CustomerSignatureUrl == null || x.CustomerSignatureUrl == string.Empty)
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        return await _context.VehicleConditionLogs
            .Where(x => x.LogType == normalizedLogType)
            .Where(IsPendingOrAssignedExpression())
            .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
            .ToListAsync();
    }

    public async Task<VehicleConditionLog?> GetActiveRequestAsync(long serviceOrderId, string normalizedLogType)
    {
        if (normalizedLogType == LogTypeCheckIn)
        {
            return await _context.VehicleConditionLogs
                .Where(x => x.ServiceOrderId == serviceOrderId && x.LogType == normalizedLogType)
                .Where(x => x.CustomerSignatureUrl == null || x.CustomerSignatureUrl == string.Empty)
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .FirstOrDefaultAsync();
        }

        return await _context.VehicleConditionLogs
            .Where(x => x.ServiceOrderId == serviceOrderId && x.LogType == normalizedLogType)
            .Where(IsPendingOrAssignedExpression())
            .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasCompletedLogAsync(long serviceOrderId, string normalizedLogType)
    {
        if (normalizedLogType == LogTypeCheckIn)
        {
            return await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == normalizedLogType &&
                    x.CustomerSignatureUrl != null &&
                    x.CustomerSignatureUrl != "");
        }

        return await _context.VehicleConditionLogs
            .AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.LogType == normalizedLogType &&
                x.DashboardWarningLights != StatusPending &&
                x.DashboardWarningLights != StatusAssigned);
    }

    public async Task<bool> HasSignedCheckInAsync(long serviceOrderId)
    {
        return await _context.VehicleConditionLogs
            .AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.LogType == LogTypeCheckIn &&
                x.CustomerSignatureUrl != null &&
                x.CustomerSignatureUrl != "");
    }

    public async Task AddAndSaveAsync(VehicleConditionLog requestLog)
    {
        _context.VehicleConditionLogs.Add(requestLog);
        await SaveChangesInTransactionAsync();
    }

    public async Task UpdateAndSaveAsync(VehicleConditionLog requestLog)
    {
        _context.VehicleConditionLogs.Update(requestLog);
        await SaveChangesInTransactionAsync();
    }

    public async Task<ServiceOrder?> FindServiceOrderAsync(long serviceOrderId)
    {
        return await _context.ServiceOrders.FindAsync(serviceOrderId);
    }

    public async Task UpdateServiceOrderAndSaveAsync(ServiceOrder serviceOrder)
    {
        _context.ServiceOrders.Update(serviceOrder);
        await SaveChangesInTransactionAsync();
    }

    private async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }

    private static Expression<Func<VehicleConditionLog, bool>> IsPendingOrAssignedExpression()
    {
        return x => x.DashboardWarningLights == StatusPending || x.DashboardWarningLights == StatusAssigned;
    }
}
