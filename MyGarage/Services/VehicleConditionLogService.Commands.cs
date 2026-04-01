using System;
using System.Threading.Tasks;
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.VehicleConditionLogs.Add(requestLog);
            await _context.SaveChangesAsync();
            return requestLog;
        }

        public async Task MarkRequestAssignedAsync(VehicleConditionLog requestLog, long technicianId)
        {
            requestLog.InspectorId = technicianId;
            requestLog.DashboardWarningLights = StatusAssigned;
            requestLog.UpdatedAt = DateTime.UtcNow;
            _context.VehicleConditionLogs.Update(requestLog);
            await _context.SaveChangesAsync();
        }

        public async Task MarkActiveRequestCompletedAsync(long serviceOrderId, string logType)
        {
            var activeRequest = await GetActiveRequestAsync(serviceOrderId, logType);
            if (activeRequest == null) return;

            activeRequest.DashboardWarningLights = StatusCompleted;
            activeRequest.UpdatedAt = DateTime.UtcNow;
            _context.VehicleConditionLogs.Update(activeRequest);
            await _context.SaveChangesAsync();
        }
    }
}
