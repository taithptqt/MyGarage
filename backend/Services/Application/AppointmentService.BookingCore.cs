using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.DTOs.Requests;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<Appointment> BookCoreAsync(
            long authUserId,
            BookAppointmentRequestDto dto,
            long? groupSessionId,
            IDbContextTransaction? externalTransaction)
        {
            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(dto.CreatedBy ?? authUserId);
            var requestedServiceIdList = ParseServiceIds(dto.RequestedServiceIds).Distinct().ToList();
            var requestedPackageIdList = ParsePackageIds(dto.RequestedPackageIds).Distinct().ToList();
            var flow = await ResolveFlowAsync(dto.BookingFlow, dto.RequestedServiceIds, dto.RequestedPackageIds);
            var effectiveRequestedServiceIds = requestedServiceIdList.Count > 0
                ? string.Join(",", requestedServiceIdList)
                : await ResolveRequestedServiceIdsForBookingAsync(dto.RequestedServiceIds, dto.RequestedPackageIds);
            var effectiveRequestedPackageIds = requestedPackageIdList.Count > 0
                ? await ResolveRequestedPackageIdsForBookingAsync(dto.RequestedPackageIds)
                : null;
            long actualCustomerId = dto.CustomerId ?? authUserId;
            var (requestedStartAt, requestedEndAt) = ResolveRequestedWindow(flow, dto);
            ValidateBookingBasics(dto, flow, requestedServiceIdList, requestedPackageIdList, requestedStartAt);
            await EnsureInsuranceCompanyExistsAsync(dto.InsuranceCompanyId);

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)requestedStartAt.DayOfWeek);
            if (opHour?.IsDayOff == true)
            {
                throw new InvalidOperationException("Chi nhánh không hoạt động vào ngày này.");
            }

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var vehicleModelId = await ResolveVehicleModelIdForBookingAsync(actualCustomerId, dto);
            await EnsureRequestedServicesCompatibleWithVehicleEngineAsync(
                vehicleModelId,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds);
            var partAvailability = await EvaluatePartAvailabilityForBookingAsync(
                dto.BranchId,
                vehicleModelId,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds);

            var plannedDurationMinutes = await ResolveScheduledDurationAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                false,
                effectiveRequestedPackageIds);

            if (flow == BookingFlowType.Fixed &&
                ShouldFallbackToFlexibleFlowForLongDuration(plannedDurationMinutes, openTime, closeTime))
            {
                flow = BookingFlowType.Flexible;
            }

            if (flow == BookingFlowType.Fixed)
            {
                var minLeadMinutes = Math.Max(15, slotStepMinutes);
                if (requestedStartAt.Date == _clock.LocalNow.Date && requestedStartAt < _clock.LocalNow.AddMinutes(minLeadMinutes))
                {
                    throw new InvalidOperationException("Thời gian hẹn phải cách hiện tại ít nhất " + minLeadMinutes + " phút.");
                }

                requestedEndAt = requestedStartAt.AddMinutes(plannedDurationMinutes);
            }
            else if (requestedEndAt <= requestedStartAt)
            {
                requestedEndAt = requestedStartAt.AddMinutes(ResolveFlexibleWindowMinutes(slotStepMinutes));
            }

            if (requestedEndAt <= requestedStartAt)
            {
                throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
            }

            ValidateTimeWindow(requestedStartAt, requestedEndAt, openTime, closeTime);
            ValidateSlotHoldOrThrow(dto, flow, requestedStartAt, requestedEndAt);

            var health = await GetBranchBookingHealthAsync(dto.BranchId, _clock.LocalNow);
            if (health.IsBookingLocked && health.LockUntil.HasValue && requestedStartAt < health.LockUntil.Value)
            {
                throw new InvalidOperationException($"Chi nhánh đang tạm khóa nhận lịch đến {health.LockUntil:HH:mm dd/MM/yyyy} do quá tải.");
            }

            var requestedCluster = await ResolveServiceClusterAsync(effectiveRequestedServiceIds, effectiveRequestedPackageIds, dto.CustomerNotes);
            if (flow == BookingFlowType.Flexible)
            {
                await EnsureFlexibleQueueCapacityAsync(dto.BranchId, requestedStartAt, requestedEndAt, dto.SlotHoldToken);
            }
            else
            {
                await EnsureFixedFlowCapacityAsync(dto.BranchId, requestedStartAt, requestedEndAt, requestedCluster, dto.SlotHoldToken);
            }

            var created = await PersistBookingAsync(
                externalTransaction,
                actorStaffId,
                dto,
                actualCustomerId,
                groupSessionId,
                requestedStartAt,
                requestedEndAt,
                flow,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds,
                requestedCluster,
                vehicleModelId,
                plannedDurationMinutes,
                slotStepMinutes,
                partAvailability);

            if (!string.IsNullOrWhiteSpace(dto.SlotHoldToken))
            {
                _slotHoldService.ConsumeHold(dto.SlotHoldToken);
            }

            return created;
        }
    }
}
