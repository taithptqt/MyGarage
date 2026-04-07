using System;
using System.Threading.Tasks;
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

        public async Task MarkServiceOrderCheckedInAsync(long serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return;
            }

            var checkedInStatus = nameof(ServiceOrderLifecycleStatus.CHECKED_IN);
            var now = DateTime.UtcNow;
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
            await _context.SaveChangesAsync();
        }
    }
}
