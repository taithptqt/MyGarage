using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VehicleConditionLogService
    {
        public async Task<VehicleConditionLog> CreateRequestAsync(long serviceOrderId, string logType)
        {
            var normalizedType = NormalizeLogType(logType);
            var requestLog = new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId,
                LogType = normalizedType,
                DashboardWarningLights = StatusPending,
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            };

            _context.VehicleConditionLogs.Add(requestLog);
            await SaveChangesInTransactionAsync();
            return requestLog;
        }

        public async Task MarkRequestAssignedAsync(VehicleConditionLog requestLog, long technicianId)
        {
            requestLog.InspectorId = technicianId;
            requestLog.DashboardWarningLights = StatusAssigned;
            requestLog.UpdatedAt = VietnamTime.Now;
            _context.VehicleConditionLogs.Update(requestLog);
            await SaveChangesInTransactionAsync();
        }

        public async Task MarkActiveRequestCompletedAsync(long serviceOrderId, string logType)
        {
            var activeRequest = await GetActiveRequestAsync(serviceOrderId, logType);
            if (activeRequest == null) return;

            activeRequest.DashboardWarningLights = StatusCompleted;
            activeRequest.UpdatedAt = VietnamTime.Now;
            _context.VehicleConditionLogs.Update(activeRequest);
            await SaveChangesInTransactionAsync();
        }

        public async Task MarkServiceOrderCheckedInAsync(long serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return;
            }

            var checkedInStatus = nameof(ServiceOrderLifecycleStatus.CHECKED_IN);
            var now = VietnamTime.Now;
            var isAlreadyCheckedIn = string.Equals(serviceOrder.Status, checkedInStatus, StringComparison.OrdinalIgnoreCase);
            if (isAlreadyCheckedIn && serviceOrder.CheckInTime.HasValue)
            {
                return;
            }

            serviceOrder.Status = checkedInStatus;
            if (!serviceOrder.CheckInTime.HasValue)
            {
                serviceOrder.CheckInTime = now;
            }
            serviceOrder.UpdatedAt = now;
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
    }
}
