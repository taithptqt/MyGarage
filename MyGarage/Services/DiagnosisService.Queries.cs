using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
        public async Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly)
        {
            var query = _context.Diagnoses.AsNoTracking().AsQueryable();

            if (serviceOrderId.HasValue)
            {
                query = query.Where(x => x.ServiceOrderId == serviceOrderId.Value);
            }

            if (requestOnly)
            {
                query = query.Where(x => x.Status == "PENDING" || x.Status == "ASSIGNED");
            }

            return await query
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId)
        {
            return await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
        }

        public async Task<bool> HasSignedCheckInAsync(long serviceOrderId)
        {
            return await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_IN" &&
                    x.CustomerSignatureUrl != null &&
                    x.CustomerSignatureUrl != "");
        }

        public async Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId)
        {
            return await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .ToListAsync();
        }

        public async Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds)
        {
            var idList = diagnosisIds?.Distinct().ToList() ?? new List<long>();
            if (idList.Count == 0) return;

            var recs = await _context.DiagnoseRecommendations
                .Where(x => idList.Contains(x.DiagnoseId))
                .ToListAsync();
            if (recs.Count == 0) return;

            _context.DiagnoseRecommendations.RemoveRange(recs);
        }

        public Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses)
        {
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count > 0)
            {
                _context.Diagnoses.RemoveRange(list);
            }
            return Task.CompletedTask;
        }


    }
}
