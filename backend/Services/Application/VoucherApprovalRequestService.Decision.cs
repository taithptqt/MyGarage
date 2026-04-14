using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherApprovalRequestService
    {
        public async Task<VoucherApprovalRequest?> ApproveAsync(long id, VoucherApprovalDecisionDto decision)
        {
            var request = await LoadPendingRequestAsync(id, "approved");
            if (request == null) return null;

            request.Status = StatusApproved;
            request.ApproverId = decision.ApproverId;
            request.UpdatedAt = VietnamTime.Now;

            if (!string.IsNullOrWhiteSpace(decision.Note))
            {
                request.RequestPayload = AppendDecisionNote(request.RequestPayload, "APPROVE_NOTE", decision.Note);
            }

            await _context.SaveChangesAsync();
            return await _voucherApprovalRequestRepository.GetDetailedByIdAsync(id);
        }

        public async Task<VoucherApprovalRequest?> RejectAsync(long id, VoucherApprovalDecisionDto decision)
        {
            var request = await LoadPendingRequestAsync(id, "rejected");
            if (request == null) return null;

            request.Status = StatusRejected;
            request.ApproverId = decision.ApproverId;
            request.RejectReason = string.IsNullOrWhiteSpace(decision.Reason) ? decision.Note : decision.Reason;
            request.UpdatedAt = VietnamTime.Now;

            await _context.SaveChangesAsync();
            return await _voucherApprovalRequestRepository.GetDetailedByIdAsync(id);
        }

        private async Task<VoucherApprovalRequest?> LoadPendingRequestAsync(long id, string actionName)
        {
            var request = await _context.VoucherApprovalRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null) return null;

            if (!string.Equals(request.Status, StatusPending, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Only pending requests can be {actionName}.");
            }

            return request;
        }

        private static string AppendDecisionNote(string? payload, string key, string note)
        {
            var sanitizedNote = note.Replace("|", "/").Trim();
            var entry = $"{key}:{sanitizedNote}";
            return string.IsNullOrWhiteSpace(payload) ? entry : $"{payload} | {entry}";
        }
    }
}
