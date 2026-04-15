using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Execution;

namespace MyGarage.Interfaces
{
    public interface ITechnicianProductivityService
    {
        Task<TechnicianProductivitySummaryDto?> GetTechnicianProductivityAsync(
            long technicianId,
            DateOnly? fromDate = null,
            DateOnly? toDate = null);

        Task<TechnicianProductivityReportDto> GetProductivityReportAsync(
            string? period = null,
            DateOnly? fromDate = null,
            DateOnly? toDate = null,
            long? branchId = null,
            int top = 30);
    }
}
