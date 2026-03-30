// Mục đích: Xử lý các bước chuyển trạng thái phê duyệt voucher như duyệt hoặc từ chối.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VoucherApprovalRequestService : GenericService<VoucherApprovalRequest>, IVoucherApprovalRequestService
    {
        private readonly IVoucherApprovalRequestRepository _voucherApprovalRequestRepository;
        private readonly GarageContext _context;

        public VoucherApprovalRequestService(IVoucherApprovalRequestRepository repository, GarageContext context)
            : base(repository)
        {
            _voucherApprovalRequestRepository = repository;
            _context = context;
        }

        public async Task<IEnumerable<VoucherApprovalRequest>> GetAllAsync(string? status = null)
        {
            return await _voucherApprovalRequestRepository.GetAllAsync(status);
        }

        public async Task<VoucherApprovalRequest?> GetDetailedByIdAsync(long id)
        {
            return await _voucherApprovalRequestRepository.GetDetailedByIdAsync(id);
        }

        public override async Task<VoucherApprovalRequest> AddAsync(VoucherApprovalRequest entity)
        {
            var now = DateTime.UtcNow;
            entity.RequestType = (entity.RequestType ?? string.Empty).Trim().ToUpperInvariant();
            entity.Status = string.IsNullOrWhiteSpace(entity.Status) ? "PENDING" : entity.Status!.Trim().ToUpperInvariant();
            entity.RiskLevel = string.IsNullOrWhiteSpace(entity.RiskLevel) ? "MEDIUM" : entity.RiskLevel!.Trim().ToUpperInvariant();
            if (entity.CreatedAt == default) entity.CreatedAt = now;
            if (entity.UpdatedAt == default) entity.UpdatedAt = now;

            return await base.AddAsync(entity);
        }

        public async Task<VoucherApprovalRequest?> ApproveAsync(long id, VoucherApprovalDecisionDto decision)
        {
            var request = await _context.VoucherApprovalRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                return null;
            }

            if (!string.Equals(request.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only pending requests can be approved.");
            }

            request.Status = "APPROVED";
            request.ApproverId = decision.ApproverId;
            request.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(decision.Note))
            {
                request.RequestPayload = AppendDecisionNote(request.RequestPayload, "APPROVE_NOTE", decision.Note);
            }

            await _context.SaveChangesAsync();
            return await _voucherApprovalRequestRepository.GetDetailedByIdAsync(id);
        }

        public async Task<VoucherApprovalRequest?> RejectAsync(long id, VoucherApprovalDecisionDto decision)
        {
            var request = await _context.VoucherApprovalRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                return null;
            }

            if (!string.Equals(request.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only pending requests can be rejected.");
            }

            request.Status = "REJECTED";
            request.ApproverId = decision.ApproverId;
            request.RejectReason = string.IsNullOrWhiteSpace(decision.Reason) ? decision.Note : decision.Reason;
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await _voucherApprovalRequestRepository.GetDetailedByIdAsync(id);
        }

        private static string AppendDecisionNote(string? payload, string key, string note)
        {
            var sanitizedNote = note.Replace("|", "/").Trim();
            var entry = $"{key}:{sanitizedNote}";
            return string.IsNullOrWhiteSpace(payload) ? entry : $"{payload} | {entry}";
        }
    }
}
