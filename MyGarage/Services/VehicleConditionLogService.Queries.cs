using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VehicleConditionLogService
    {
        private const string StatusPending = "PENDING";
        private const string StatusAssigned = "ASSIGNED";
        private const string StatusCompleted = "COMPLETED";
        private const string LogTypeCheckIn = "CHECK_IN";

        public async Task<IReadOnlyList<VehicleConditionLog>> GetByOrderAsync(long serviceOrderId)
        {
            return await _context.VehicleConditionLogs
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<VehicleConditionLog>> GetActiveRequestsAsync(string logType)
        {
            var normalizedType = NormalizeLogType(logType);

            return await _context.VehicleConditionLogs
                .Where(x => x.LogType == normalizedType)
                .Where(IsPendingOrAssignedExpression())
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<VehicleConditionLog?> GetActiveRequestAsync(long serviceOrderId, string logType)
        {
            var normalizedType = NormalizeLogType(logType);

            return await _context.VehicleConditionLogs
                .Where(x => x.ServiceOrderId == serviceOrderId && x.LogType == normalizedType)
                .Where(IsPendingOrAssignedExpression())
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> HasCompletedLogAsync(long serviceOrderId, string logType)
        {
            var normalizedType = NormalizeLogType(logType);

            return await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == normalizedType &&
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

        private static string NormalizeLogType(string? logType)
        {
            return (logType ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static System.Linq.Expressions.Expression<Func<VehicleConditionLog, bool>> IsPendingOrAssignedExpression()
        {
            return x => x.DashboardWarningLights == StatusPending || x.DashboardWarningLights == StatusAssigned;
        }
    }
}
