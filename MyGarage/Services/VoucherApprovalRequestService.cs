// Mục đích: Xử lý các bươc chuyển trạng thái phê duyệt voucher như duyệt hoặc từ chđi.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherApprovalRequestService : GenericService<VoucherApprovalRequest>, IVoucherApprovalRequestService
    {
        private const string StatusPending = "PENDING";
        private const string StatusApproved = "APPROVED";
        private const string StatusRejected = "REJECTED";

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
            entity.Status = string.IsNullOrWhiteSpace(entity.Status) ? StatusPending : entity.Status!.Trim().ToUpperInvariant();
            entity.RiskLevel = string.IsNullOrWhiteSpace(entity.RiskLevel) ? "MEDIUM" : entity.RiskLevel!.Trim().ToUpperInvariant();
            if (entity.CreatedAt == default) entity.CreatedAt = now;
            if (entity.UpdatedAt == default) entity.UpdatedAt = now;

            return await base.AddAsync(entity);
        }
    }
}


