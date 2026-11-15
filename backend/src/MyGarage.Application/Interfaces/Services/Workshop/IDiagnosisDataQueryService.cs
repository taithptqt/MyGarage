using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisDataQueryService
    {
        Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null);
        Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId);
        Task<bool> HasSignedCheckInAsync(long serviceOrderId);
        Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId);
        Task<IReadOnlyList<Diagnosis>> GetLatestWorkflowDiagnosesByServiceOrderAsync(long serviceOrderId);
        Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId);
        Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(long? branchId, DateTime? startDate, DateTime? endDate);
        Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(long? branchId, long? teamId, bool includeCompleted, string? viewMode = null);
        Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId);
        Task<IReadOnlyDictionary<long, List<DiagnosisAssignment>>> GetAssignmentHistoryByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds);
    }
}
