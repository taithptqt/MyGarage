using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.DTOs.Requests;

namespace MyGarage.Services
{
    public class QcInspectionService : GenericService<QcInspection>, IQcInspectionService
    {
        private readonly IGarageContext _context;

        public QcInspectionService(IQcInspectionRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public async Task<bool> PassAsync(long id, QcPassRequestDto request)
        {
            var inspection = await _context.QcInspections.FirstOrDefaultAsync(x => x.Id == id);
            if (inspection == null) return false;

            await ExecuteInTransactionAsync(async () =>
            {
                inspection.Status = nameof(QcInspectionStatusCode.PASS);
                inspection.InspectedAt = VietnamTime.Now;
                inspection.QcStaffId = request.InspectorId;
                inspection.OverallNote = request.Notes;
                await _repository.UpdateAsync(id, inspection);

                var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == inspection.ServiceOrderId);
                if (order != null)
                {
                    order.Qcstatus = nameof(ServiceOrderQcStatusCode.PASS);
                    order.QcverifiedAt = inspection.InspectedAt;
                    order.QcinspectorId = request.InspectorId;
                    
                    // Automatically move to READY for handover if QC passes
                    order.Status = nameof(ServiceOrderLifecycleStatus.READY);
                    order.UpdatedAt = VietnamTime.Now;
                }

                await _context.SaveChangesAsync();
            });

            return true;
        }

        public async Task<bool> FailAsync(long id, QcFailRequestDto request)
        {
            var inspection = await _context.QcInspections.FirstOrDefaultAsync(x => x.Id == id);
            if (inspection == null) return false;

            await ExecuteInTransactionAsync(async () =>
            {
                inspection.Status = nameof(QcInspectionStatusCode.FAIL);
                inspection.InspectedAt = VietnamTime.Now;
                inspection.QcStaffId = request.InspectorId;
                inspection.OverallNote = request.Reason;
                await _repository.UpdateAsync(id, inspection);

                var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == inspection.ServiceOrderId);
                if (order != null)
                {
                    order.Qcstatus = nameof(ServiceOrderQcStatusCode.FAIL);
                    order.QcinspectorId = request.InspectorId;
                    
                    // Revert to REPAIRING status on QC failure
                    order.Status = nameof(ServiceOrderLifecycleStatus.REPAIRING);
                    order.UpdatedAt = VietnamTime.Now;
                }

                await _context.SaveChangesAsync();
            });

            return true;
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
