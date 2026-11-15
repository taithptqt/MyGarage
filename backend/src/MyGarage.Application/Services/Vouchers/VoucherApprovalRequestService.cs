// Mục đích: Xử lý các bước chuyển trạng thái phê duyệt voucher như duyệt hoặc từ chối.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Vouchers;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherApprovalRequestService : GenericService<VoucherApprovalRequest>, IVoucherApprovalRequestService
    {
        private const string StatusPending = nameof(VoucherApprovalRequestStatusCode.PENDING);
        private const string StatusApproved = nameof(VoucherApprovalRequestStatusCode.APPROVED);
        private const string StatusRejected = nameof(VoucherApprovalRequestStatusCode.REJECTED);

        private readonly IVoucherApprovalRequestRepository _voucherApprovalRequestRepository;
        private readonly IVoucherStore _voucherStore;
        private readonly IMapper _mapper;

        public VoucherApprovalRequestService(IVoucherApprovalRequestRepository repository, IVoucherStore voucherStore, IMapper mapper)
            : base(repository)
        {
            _voucherApprovalRequestRepository = repository;
            _voucherStore = voucherStore;
            _mapper = mapper;
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
            var now = VietnamTime.Now;
            entity.RequestType = (entity.RequestType ?? string.Empty).Trim().ToUpperInvariant();
            entity.Status = string.IsNullOrWhiteSpace(entity.Status) ? StatusPending : entity.Status!.Trim().ToUpperInvariant();
            entity.RiskLevel = string.IsNullOrWhiteSpace(entity.RiskLevel) ? "MEDIUM" : entity.RiskLevel!.Trim().ToUpperInvariant();
            if (entity.CreatedAt == default) entity.CreatedAt = now;
            if (entity.UpdatedAt == default) entity.UpdatedAt = now;

            return await base.AddAsync(entity);
        }

        public List<VoucherApprovalRequestResponseDto> MapToDtos(IEnumerable<VoucherApprovalRequest> items)
        {
            return _mapper.Map<List<VoucherApprovalRequestResponseDto>>(items);
        }

        public async Task<VoucherApprovalRequestResponseDto?> GetDetailedDtoByIdAsync(long id)
        {
            var item = await GetDetailedByIdAsync(id);
            return item == null ? null : _mapper.Map<VoucherApprovalRequestResponseDto>(item);
        }

        public async Task<VoucherApprovalRequestResponseDto> CreateDtoAsync(VoucherApprovalRequestCreateDto dto)
        {
            var entity = _mapper.Map<VoucherApprovalRequest>(dto);
            var created = await AddAsync(entity);
            return _mapper.Map<VoucherApprovalRequestResponseDto>(created);
        }

        public async Task<VoucherApprovalRequestResponseDto?> ApproveDtoAsync(long id, VoucherApprovalDecisionDto decision)
        {
            var updated = await ApproveAsync(id, decision);
            return updated == null ? null : _mapper.Map<VoucherApprovalRequestResponseDto>(updated);
        }

        public async Task<VoucherApprovalRequestResponseDto?> RejectDtoAsync(long id, VoucherApprovalDecisionDto decision)
        {
            var updated = await RejectAsync(id, decision);
            return updated == null ? null : _mapper.Map<VoucherApprovalRequestResponseDto>(updated);
        }
    }
}
