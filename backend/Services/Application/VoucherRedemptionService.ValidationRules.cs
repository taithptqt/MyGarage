using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        private VoucherValidationResultResponseDto? ValidateVoucherState(Voucher voucher, DateTime now)
        {
            if (voucher.IsDeleted == true)
            {
                return BuildFail("VCH_DELETED", "Voucher is deleted.");
            }

            var status = (voucher.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(status) && status != nameof(VoucherStatusCode.ACTIVE))
            {
                return BuildFail(MapStatusReason(status), $"Voucher status is {status}.", voucher);
            }

            if (voucher.IsActive == false)
            {
                return BuildFail("VCH_INACTIVE", "Voucher is inactive.", voucher);
            }

            if (now < voucher.StartDate)
            {
                return BuildFail("VCH_NOT_STARTED", "Voucher is not started yet.", voucher);
            }

            if (now > voucher.EndDate)
            {
                return BuildFail("VCH_EXPIRED", "Voucher expired.", voucher, true);
            }

            if (!IsInValidTimeRange(voucher.ValidFrom, voucher.ValidTo, now.TimeOfDay))
            {
                return BuildFail("VCH_INVALID_TIME_RANGE", "Voucher is outside valid time range.", voucher);
            }

            if (!IsDayAllowed(voucher.ValidDaysOfWeek, now.DayOfWeek))
            {
                return BuildFail("VCH_INVALID_DAY", "Voucher is not valid for today.", voucher);
            }

            return null;
        }

        private async Task<VoucherValidationResultResponseDto?> ValidateUsageConstraintsAsync(
            Voucher voucher,
            VoucherRedemptionRequestDto request)
        {
            if (voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0
                && (voucher.UsedCount ?? 0) >= voucher.UsageLimit.Value)
            {
                return BuildFail("VCH_QUOTA_EXHAUSTED", "Voucher usage limit exhausted.", voucher, true);
            }

            if (voucher.MaxUsesPerCustomer > 0)
            {
                if (!request.CustomerId.HasValue)
                {
                    return BuildFail("VCH_CUSTOMER_REQUIRED", "Customer is required for this voucher.", voucher);
                }

                var customerUsage = await _context.VoucherRedemptions
                    .CountAsync(x => x.VoucherId == voucher.Id
                        && x.CustomerId == request.CustomerId
                        && x.RedemptionStatus == RedemptionStatusRedeemed);

                if (customerUsage >= voucher.MaxUsesPerCustomer)
                {
                    return BuildFail("VCH_CUSTOMER_LIMIT_REACHED", "Customer usage limit reached.", voucher);
                }
            }

            if (voucher.MaxUsesPerVehicle > 0)
            {
                if (!request.VehicleId.HasValue)
                {
                    return BuildFail("VCH_VEHICLE_REQUIRED", "Vehicle is required for this voucher.", voucher);
                }

                var vehicleUsage = await _context.VoucherRedemptions
                    .CountAsync(x => x.VoucherId == voucher.Id
                        && x.VehicleId == request.VehicleId
                        && x.RedemptionStatus == RedemptionStatusRedeemed);

                if (vehicleUsage >= voucher.MaxUsesPerVehicle)
                {
                    return BuildFail("VCH_VEHICLE_LIMIT_REACHED", "Vehicle usage limit reached.", voucher);
                }
            }

            if (voucher.FirstUseOnly == true)
            {
                if (!request.CustomerId.HasValue)
                {
                    return BuildFail("VCH_CUSTOMER_REQUIRED", "Customer is required for first-use voucher.", voucher);
                }

                var customerId = request.CustomerId.Value;
                var hasPriorServiceOrder = await _context.ServiceOrders.AnyAsync(x =>
                    x.CustomerId == customerId &&
                    (!request.ServiceOrderId.HasValue || x.Id != request.ServiceOrderId.Value));
                var hasPriorRetailOrder = await _context.RetailOrders.AnyAsync(x =>
                    x.CustomerId == customerId &&
                    (!request.RetailOrderId.HasValue || x.Id != request.RetailOrderId.Value));

                if (hasPriorServiceOrder || hasPriorRetailOrder)
                {
                    return BuildFail("VCH_FIRST_USE_ONLY", "Voucher is only for first use customers.", voucher);
                }
            }

            return null;
        }

        private static VoucherValidationResultResponseDto BuildEligibilitySuccess(
            Voucher voucher,
            decimal eligibleAmount,
            decimal discount)
        {
            return new VoucherValidationResultResponseDto
            {
                IsEligible = true,
                RequiresApproval = false,
                VoucherId = voucher.Id,
                VoucherCode = voucher.Code,
                EligibleAmount = eligibleAmount,
                DiscountPreview = discount,
                Message = "Voucher is eligible."
            };
        }

    }
}
