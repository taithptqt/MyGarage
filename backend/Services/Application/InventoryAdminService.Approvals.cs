using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        private const string ApprovalStatusApproved = nameof(ApprovalRequestStatusCode.APPROVED);
        private const string ApprovalStatusRejected = nameof(ApprovalRequestStatusCode.REJECTED);
        private const string AuditStatusCompleted = nameof(InventoryAuditStatusCode.COMPLETED);
        private const string AuditStatusCanceled = nameof(InventoryAuditStatusCode.CANCELED);

        public async Task<IReadOnlyList<object>> GetApprovalsAsync(string? status)
        {
            var query = _context.ApprovalRequests
                .AsNoTracking()
                .Include(a => a.RequestedByNavigation)
                .Include(a => a.Approver)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(a => a.Status != null && a.Status.ToUpper() == normalized);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => (object)new
                {
                    id = a.Id,
                    requestType = a.RequestType,
                    status = a.Status,
                    reason = a.Reason,
                    rejectReason = a.RejectReason,
                    requestedBy = a.RequestedBy,
                    requestedByName = a.RequestedByNavigation.FullName,
                    approverId = a.ApproverId,
                    approverName = a.Approver != null ? a.Approver.FullName : null,
                    createdAt = a.CreatedAt,
                    updatedAt = a.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> ApproveRequestAsync(long id, long? approverId, string? note)
        {
            var request = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id);
            if (request == null) return false;

            request.Status = ApprovalStatusApproved;
            request.ApproverId = approverId;
            request.UpdatedAt = VietnamTime.Now;
            if (!string.IsNullOrWhiteSpace(note)) request.Reason = note.Trim();

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectRequestAsync(long id, long? approverId, string? note)
        {
            var request = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id);
            if (request == null) return false;

            request.Status = ApprovalStatusRejected;
            request.ApproverId = approverId;
            request.RejectReason = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            request.UpdatedAt = VietnamTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<object>> GetAuditsAsync(string? status)
        {
            var query = _context.InventoryAudits
                .AsNoTracking()
                .Include(a => a.Warehouse)
                    .ThenInclude(w => w.Branch)
                .Include(a => a.InventoryAuditDetails)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(a => a.Status.ToUpper() == normalized);
            }

            return await query
                .OrderByDescending(a => a.AuditDate)
                .Select(a => (object)new
                {
                    id = a.Id,
                    status = a.Status,
                    auditDate = a.AuditDate,
                    warehouseId = a.WarehouseId,
                    warehouseName = a.Warehouse.Name,
                    branchName = a.Warehouse.Branch.Name,
                    approvedBy = a.ApprovedBy,
                    createdBy = a.CreatedBy,
                    detailCount = a.InventoryAuditDetails.Count,
                    differenceTotal = a.InventoryAuditDetails.Sum(d => Math.Abs(d.DifferenceQty ?? 0))
                })
                .ToListAsync();
        }

        public async Task<bool> ApproveAuditAsync(long id, long? approverId)
        {
            var audit = await _context.InventoryAudits.FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return false;

            audit.Status = AuditStatusCompleted;
            audit.ApprovedBy = approverId;
            audit.UpdatedAt = VietnamTime.Now;
            audit.UpdatedBy = approverId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectAuditAsync(long id, long? approverId)
        {
            var audit = await _context.InventoryAudits.FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return false;

            audit.Status = AuditStatusCanceled;
            audit.UpdatedAt = VietnamTime.Now;
            audit.UpdatedBy = approverId;
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
