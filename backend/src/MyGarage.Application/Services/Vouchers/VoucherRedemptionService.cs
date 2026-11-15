// Mục đích: Kiểm tra điều kiện áp dụng voucher và xử lý giao dịch redeem/reverse.
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
    public class VoucherRedemptionService : GenericService<VoucherRedemption>, IVoucherRedemptionService
    {
        private const string RedemptionStatusRedeemed = nameof(VoucherRedemptionStatusCode.REDEEMED);
        private const string ValidationStatusValidated = nameof(VoucherRedemptionValidationStatusCode.VALIDATED);
        private const string VoucherStatusExhausted = nameof(VoucherStatusCode.EXHAUSTED);

        private readonly IVoucherRedemptionRepository _voucherRedemptionRepository;
        private readonly IVoucherStore _voucherStore;
        private readonly IVoucherEligibilityService _eligibilityService;
        private readonly IVoucherReversalService _reversalService;
        private readonly IMapper? _mapper;

        public VoucherRedemptionService(
            IVoucherRedemptionRepository repository,
            IVoucherStore voucherStore,
            IVoucherEligibilityService eligibilityService,
            IVoucherReversalService reversalService,
            IMapper? mapper = null)
            : base(repository)
        {
            _voucherRedemptionRepository = repository;
            _voucherStore = voucherStore;
            _eligibilityService = eligibilityService ?? throw new ArgumentNullException(nameof(eligibilityService));
            _reversalService = reversalService ?? throw new ArgumentNullException(nameof(reversalService));
            _mapper = mapper;
        }

        public async Task<IEnumerable<VoucherRedemption>> GetAllAsync(string? status = null)
        {
            return await _voucherRedemptionRepository.GetAllAsync(status);
        }

        public async Task<VoucherRedemption?> GetDetailedByIdAsync(long id)
        {
            return await _voucherRedemptionRepository.GetDetailedByIdAsync(id);
        }

        public Task<VoucherValidationResultResponseDto> CheckEligibilityAsync(VoucherRedemptionRequestDto request)
        {
            return _eligibilityService.CheckEligibilityAsync(request);
        }

        public async Task<VoucherRedeemResultResponseDto> RedeemAsync(VoucherRedemptionRequestDto request)
        {
            // Internal keys protect retry after an unknown commit result. Legacy client retries
            // without a key are separate requests and cannot be deduplicated reliably.
            var operationKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? "server-" + Guid.NewGuid().ToString("N")
                : request.IdempotencyKey.Trim();

            return await _voucherStore.ExecuteVoucherTransactionAsync(
                "Voucher redemption cannot run inside an existing transaction.",
                async ct =>
                {
                    // SQL equality for a varchar key depends on the database collation; a
                    // hash of a .NET-normalized string can map equal keys to different locks.
                    // Serialize key lookup/insertion across vouchers until the schema has a
                    // tested uniqueness constraint matching the database collation.
                    await _voucherStore.AcquireVoucherOperationLockAsync("idempotency-keys", ct);
                    var existing = await _voucherRedemptionRepository.GetByIdempotencyKeyAsync(operationKey);
                    if (existing != null)
                    {
                        if (IsIdempotencyConflict(existing, request))
                        {
                            return new VoucherRedeemResultResponseDto
                            {
                                Success = false,
                                ReasonCode = "VCH_IDEMPOTENCY_CONFLICT",
                                Message = "Idempotency key is already used for a different request."
                            };
                        }

                        return new VoucherRedeemResultResponseDto
                        {
                            Success = string.Equals(existing.RedemptionStatus, RedemptionStatusRedeemed, StringComparison.OrdinalIgnoreCase),
                            Message = "Duplicate request was deduplicated.",
                            Redemption = VoucherRedemptionHelpers.ToResponse(existing)
                        };
                    }

                    var identity = await _voucherStore.FindVoucherIdAsync(request.VoucherId, request.VoucherCode, ct);
                    if (!identity.HasValue)
                    {
                        return new VoucherRedeemResultResponseDto
                        {
                            Success = false,
                            ReasonCode = "VCH_NOT_FOUND",
                            Message = "Voucher not found."
                        };
                    }

                    await _voucherStore.AcquireVoucherOperationLockAsync("voucher:" + identity.Value, ct);
                    var validation = await CheckEligibilityAsync(request);
                    if (!validation.IsEligible)
                    {
                        return new VoucherRedeemResultResponseDto
                        {
                            Success = false,
                            RequiresApproval = validation.RequiresApproval,
                            ReasonCode = validation.ReasonCode,
                            Message = validation.Message
                        };
                    }

                    var voucher = await _voucherStore.GetVoucherByIdRequiredAsync(identity.Value, ct);
                    var now = VietnamTime.Now;
                    var discount = validation.DiscountPreview.GetValueOrDefault(0m);
                    var eligibleAmount = validation.EligibleAmount.GetValueOrDefault(0m);
                    var redemption = BuildRedemption(voucher, request, eligibleAmount, discount, operationKey, now);

                    ApplyVoucherRedemptionUsage(voucher, discount, now, request.AppliedBy);

                    await _voucherStore.AddRedemptionAndSaveAsync(redemption, ct);

                    return new VoucherRedeemResultResponseDto
                    {
                        Success = true,
                        Message = "Voucher redeemed successfully.",
                        Redemption = VoucherRedemptionHelpers.ToResponse(redemption)
                    };
                });
        }

        public Task<VoucherRedeemResultResponseDto> ReverseAsync(long redemptionId, VoucherRedemptionReverseDto request)
        {
            return _reversalService.ReverseAsync(redemptionId, request);
        }

        private static bool IsIdempotencyConflict(VoucherRedemption existing, VoucherRedemptionRequestDto request)
        {
            return existing.BranchId != request.BranchId ||
                existing.AppliedBy != request.AppliedBy ||
                existing.CustomerId != request.CustomerId ||
                existing.VehicleId != request.VehicleId ||
                existing.ServiceOrderId != request.ServiceOrderId ||
                existing.RetailOrderId != request.RetailOrderId ||
                existing.InvoiceId != request.InvoiceId ||
                existing.PaymentMethodId != request.PaymentMethodId ||
                (request.VoucherId.HasValue && existing.VoucherId != request.VoucherId.Value) ||
                (!string.IsNullOrWhiteSpace(request.VoucherCode) && !string.Equals(existing.VoucherCode, request.VoucherCode.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!request.InvoiceId.HasValue && request.EligibleAmount.HasValue && existing.EligibleAmount != request.EligibleAmount.Value);
        }

        private static VoucherRedemption BuildRedemption(
            Voucher voucher,
            VoucherRedemptionRequestDto request,
            decimal eligibleAmount,
            decimal discount,
            string operationKey,
            DateTime now)
        {
            return new VoucherRedemption
            {
                VoucherId = voucher.Id,
                VoucherCode = voucher.Code,
                CustomerId = request.CustomerId,
                VehicleId = request.VehicleId,
                BranchId = request.BranchId,
                ServiceOrderId = request.ServiceOrderId,
                RetailOrderId = request.RetailOrderId,
                InvoiceId = request.InvoiceId,
                PaymentMethodId = request.PaymentMethodId,
                ValidationStatus = ValidationStatusValidated,
                RedemptionStatus = RedemptionStatusRedeemed,
                EligibleAmount = eligibleAmount,
                DiscountAmount = discount,
                BudgetImpact = discount,
                AppliedBy = request.AppliedBy,
                IdempotencyKey = operationKey,
                AppliedAt = now,
                RedeemedAt = now,
                CreatedAt = now
            };
        }

        private static void ApplyVoucherRedemptionUsage(Voucher voucher, decimal discount, DateTime now, long? appliedBy)
        {
            voucher.UsedCount = (voucher.UsedCount ?? 0) + 1;
            voucher.RedeemedCount += 1;
            voucher.BudgetUsed += discount;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = appliedBy;

            var exhaustedByUsage = voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0 && voucher.UsedCount >= voucher.UsageLimit.Value;
            var exhaustedByBudget = voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value;
            if (exhaustedByUsage || exhaustedByBudget)
            {
                voucher.Status = VoucherStatusExhausted;
                voucher.IsActive = false;
            }
        }

        public List<VoucherRedemptionResponseDto> MapToDtos(IEnumerable<VoucherRedemption> items)
        {
            var mapper = _mapper ?? throw new InvalidOperationException("IMapper is required for DTO mapping.");
            return mapper.Map<List<VoucherRedemptionResponseDto>>(items);
        }

        public async Task<VoucherRedemptionResponseDto?> GetDetailedDtoByIdAsync(long id)
        {
            var item = await GetDetailedByIdAsync(id);
            if (item == null)
            {
                return null;
            }

            var mapper = _mapper ?? throw new InvalidOperationException("IMapper is required for DTO mapping.");
            return mapper.Map<VoucherRedemptionResponseDto>(item);
        }
    }
}
