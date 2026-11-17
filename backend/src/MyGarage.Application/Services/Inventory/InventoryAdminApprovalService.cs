using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Inventory;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class InventoryAdminApprovalService : IInventoryAdminApprovalService
    {
        private const string ApprovalStatusApproved = nameof(ApprovalRequestStatusCode.APPROVED);
        private const string ApprovalStatusRejected = nameof(ApprovalRequestStatusCode.REJECTED);
        private const string AuditStatusCompleted = nameof(InventoryAuditStatusCode.COMPLETED);
        private const string AuditStatusCanceled = nameof(InventoryAuditStatusCode.CANCELED);
        private readonly IInventoryStore _store;
        public InventoryAdminApprovalService(IInventoryStore store) => _store = store;

        public async Task<IReadOnlyList<object>> GetApprovalsAsync(string? status)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var rows = await _store.GetApprovalRequestsAsync(normalized);

            return rows
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
                .ToList();
        }

        public async Task<bool> ApproveRequestAsync(long id, long? approverId, string? note)
        {
            var request = await _store.GetApprovalRequestByIdAsync(id);
            if (request == null) return false;

            request.Status = ApprovalStatusApproved;
            request.ApproverId = approverId;
            request.UpdatedAt = VietnamTime.Now;
            if (!string.IsNullOrWhiteSpace(note)) request.Reason = note.Trim();

            await SaveChangesInTransactionAsync();
            return true;
        }

        public async Task<bool> RejectRequestAsync(long id, long? approverId, string? note)
        {
            var request = await _store.GetApprovalRequestByIdAsync(id);
            if (request == null) return false;

            request.Status = ApprovalStatusRejected;
            request.ApproverId = approverId;
            request.RejectReason = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            request.UpdatedAt = VietnamTime.Now;

            await SaveChangesInTransactionAsync();
            return true;
        }

        public async Task<IReadOnlyList<object>> GetAuditsAsync(string? status)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var rows = await _store.GetInventoryAuditsAsync(normalized);

            return rows
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
                .ToList();
        }

        public async Task<bool> ApproveAuditAsync(long id, long? approverId)
        {
            var audit = await _store.GetInventoryAuditByIdAsync(id);
            if (audit == null) return false;

            audit.Status = AuditStatusCompleted;
            audit.ApprovedBy = approverId;
            audit.UpdatedAt = VietnamTime.Now;
            audit.UpdatedBy = approverId;
            await SaveChangesInTransactionAsync();
            return true;
        }

        public async Task<bool> RejectAuditAsync(long id, long? approverId)
        {
            var audit = await _store.GetInventoryAuditByIdAsync(id);
            if (audit == null) return false;

            audit.Status = AuditStatusCanceled;
            audit.UpdatedAt = VietnamTime.Now;
            audit.UpdatedBy = approverId;
            await SaveChangesInTransactionAsync();
            return true;
        }

        private Task SaveChangesInTransactionAsync()
            => _store.ExecuteInTransactionAsync(ct => _store.SaveChangesAsync(ct));

    }
}
