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
        private const string DiagnosisStatusPending = "PENDING";
        private const string DiagnosisStatusAssigned = "ASSIGNED";
        private const string DiagnosisStatusCompleted = "COMPLETED";
        private const string ServiceOrderStatusInspecting = "INSPECTING";
        private const string ServiceOrderStatusNew = "NEW";

        public async Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder)
        {
            if (serviceOrder.ServiceAdvisorId.HasValue)
            {
                var advisorExists = await _context.Staffs.AnyAsync(x => x.Id == serviceOrder.ServiceAdvisorId.Value);
                if (advisorExists)
                {
                    return serviceOrder.ServiceAdvisorId.Value;
                }
            }

            var firstStaffId = await _context.Staffs
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            if (firstStaffId <= 0)
            {
                throw new InvalidOperationException("Kh\u00f4ng c\u00f3 nh\u00e2n s\u1ef1 trong h\u1ec7 th\u1ed1ng \u0111\u1ec3 kh\u1edfi t\u1ea1o y\u00eau c\u1ea7u ch\u1ea9n \u0111o\u00e1n.");
            }

            return firstStaffId;
        }

        public async Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(long serviceOrderId, long technicianId, IReadOnlyList<string> symptoms)
        {
            var now = DateTime.UtcNow;
            var items = (symptoms ?? Array.Empty<string>())
                .Select(symptom => new Diagnosis
                {
                    ServiceOrderId = serviceOrderId,
                    TechnicianId = technicianId,
                    Symptom = symptom,
                    Status = DiagnosisStatusPending,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            _context.Diagnoses.AddRange(items);
            await _context.SaveChangesAsync();
            return items;
        }

        public async Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status)
        {
            serviceOrder.Status = status;
            serviceOrder.UpdatedAt = DateTime.UtcNow;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId)
        {
            return await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.Status == DiagnosisStatusPending || x.Status == DiagnosisStatusAssigned)
                .ToListAsync();
        }

        public async Task<bool> TechnicianExistsAsync(long technicianId)
        {
            return await _context.Staffs.AnyAsync(x => x.Id == technicianId);
        }

        public async Task AssignDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            foreach (var diagnosis in list)
            {
                diagnosis.TechnicianId = technicianId;
                diagnosis.Status = DiagnosisStatusAssigned;
                diagnosis.UpdatedAt = now;
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses)
        {
            var activeIds = activeDiagnoses.Select(x => x.Id).ToList();
            await RemoveRecommendationsByDiagnosisIdsAsync(activeIds);
            _context.Diagnoses.RemoveRange(activeDiagnoses);

            if (serviceOrder.Status == ServiceOrderStatusInspecting)
            {
                serviceOrder.Status = ServiceOrderStatusNew;
            }

            serviceOrder.UpdatedAt = DateTime.UtcNow;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<Diagnosis?> GetDiagnosisEntityAsync(long id)
        {
            return await _context.Diagnoses.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task CompleteDiagnosisAsync(Diagnosis diagnosis)
        {
            diagnosis.Status = DiagnosisStatusCompleted;
            diagnosis.DiagnosedAt = DateTime.UtcNow;
            diagnosis.UpdatedAt = DateTime.UtcNow;
            _context.Diagnoses.Update(diagnosis);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId)
        {
            var allStatus = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Select(x => x.Status)
                .ToListAsync();
            return allStatus.Count > 0 && allStatus.All(s => s == DiagnosisStatusCompleted);
        }
    }
}