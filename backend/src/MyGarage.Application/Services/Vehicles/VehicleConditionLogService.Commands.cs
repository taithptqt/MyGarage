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
                CreatedAt = VietnamTime.Now,
                UpdatedAt = VietnamTime.Now
            };

            await _store.AddAndSaveAsync(requestLog);
            return requestLog;
        }

        public async Task MarkRequestAssignedAsync(VehicleConditionLog requestLog, long technicianId)
        {
            requestLog.InspectorId = technicianId;
            requestLog.DashboardWarningLights = StatusAssigned;
            requestLog.UpdatedAt = VietnamTime.Now;
            await _store.UpdateAndSaveAsync(requestLog);
        }

        public async Task MarkActiveRequestCompletedAsync(long serviceOrderId, string logType)
        {
            var activeRequest = await GetActiveRequestAsync(serviceOrderId, logType);
            if (activeRequest == null) return;

            activeRequest.DashboardWarningLights = StatusCompleted;
            activeRequest.UpdatedAt = VietnamTime.Now;
            await _store.UpdateAndSaveAsync(activeRequest);
        }

        public async Task MarkServiceOrderCheckedInAsync(long serviceOrderId)
        {
            var serviceOrder = await _store.FindServiceOrderAsync(serviceOrderId);
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
            await _store.UpdateServiceOrderAndSaveAsync(serviceOrder);
        }
    }
}
