using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VehicleConditionLogService
    {
        private const string StatusPending = nameof(DiagnosisStatus.PENDING);
        private const string StatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
        private const string StatusCompleted = nameof(DiagnosisStatus.COMPLETED);

        public async Task<IReadOnlyList<VehicleConditionLog>> GetByOrderAsync(long serviceOrderId)
        {
            return await _store.GetByOrderAsync(serviceOrderId);
        }

        public async Task<IReadOnlyList<VehicleConditionLogResponseDto>> GetByOrderDtosAsync(long serviceOrderId)
        {
            var items = await GetByOrderAsync(serviceOrderId);
            return _mapper.Map<IReadOnlyList<VehicleConditionLogResponseDto>>(items);
        }

        public async Task<IReadOnlyList<VehicleConditionLog>> GetActiveRequestsAsync(string logType)
        {
            var normalizedType = NormalizeLogType(logType);
            return await _store.GetActiveRequestsAsync(normalizedType);
        }

        public async Task<IReadOnlyList<VehicleConditionLogResponseDto>> GetActiveRequestDtosAsync(string logType)
        {
            var items = await GetActiveRequestsAsync(logType);
            return _mapper.Map<IReadOnlyList<VehicleConditionLogResponseDto>>(items);
        }

        public async Task<VehicleConditionLog?> GetActiveRequestAsync(long serviceOrderId, string logType)
        {
            var normalizedType = NormalizeLogType(logType);
            return await _store.GetActiveRequestAsync(serviceOrderId, normalizedType);
        }

        public async Task<bool> HasCompletedLogAsync(long serviceOrderId, string logType)
        {
            var normalizedType = NormalizeLogType(logType);
            return await _store.HasCompletedLogAsync(serviceOrderId, normalizedType);
        }

        public async Task<bool> HasSignedCheckInAsync(long serviceOrderId)
        {
            return await _store.HasSignedCheckInAsync(serviceOrderId);
        }

        private static string NormalizeLogType(string? logType)
        {
            return (logType ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
