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
            long? bookingGroupId,
            IDbContextTransaction? externalTransaction)
        {
            var actorStaffId = await _bookingGroupService.ResolveStaffActorIdAsync(dto.CreatedBy ?? authUserId);
            var requestedServiceIdList = ParseServiceIds(dto.RequestedServiceIds).Distinct().ToList();
            var flow = await ResolveFlowAsync(dto.BookingFlow, dto.RequestedServiceIds);
            var effectiveRequestedServiceIds = requestedServiceIdList.Count > 0
                ? string.Join(",", requestedServiceIdList)
                : await ResolveRequestedServiceIdsForBookingAsync(dto.RequestedServiceIds);
            long actualCustomerId = dto.CustomerId ?? authUserId;
            var (requestedStartAt, requestedEndAt) = ResolveRequestedWindow(flow, dto);
            ValidateBookingBasics(dto, flow, requestedServiceIdList, requestedStartAt);

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)requestedStartAt.DayOfWeek);
            if (opHour?.IsDayOff == true) throw new InvalidOperationException("Chi nhánh không ho?t d?ng vào ngày này.");

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            if (flow == BookingFlowType.Fixed)
            {
                var minLeadMinutes = Math.Max(15, slotStepMinutes);
                if (requestedStartAt.Date == _clock.LocalNow.Date && requestedStartAt < _clock.LocalNow.AddMinutes(minLeadMinutes))
                    throw new InvalidOperationException("Th?i gian h?n ph?i cách hi?n t?i ít nh?t " + minLeadMinutes + " phút.");
            }

            var plannedDurationMinutes = await ResolveScheduledDurationAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                isParallel: false);
            requestedEndAt = requestedStartAt.AddMinutes(plannedDurationMinutes);

            if (requestedEndAt <= requestedStartAt) throw new InvalidOperationException("Gi? k?t thúc ph?i sau gi? b?t d?u.");
            ValidateTimeWindow(requestedStartAt, requestedEndAt, openTime, closeTime);
            ValidateSlotHoldOrThrow(dto, flow, requestedStartAt, requestedEndAt);

            var health = await GetBranchBookingHealthAsync(dto.BranchId, _clock.LocalNow);
            if (health.IsBookingLocked && health.LockUntil.HasValue && requestedStartAt < health.LockUntil.Value)
                throw new InvalidOperationException($"Chi nhánh dang t?m khóa nh?n l?ch d?n {health.LockUntil:HH:mm dd/MM/yyyy} do quá t?i.");

            var requestedCluster = await ResolveServiceClusterAsync(effectiveRequestedServiceIds, dto.CustomerNotes);
            await EnsureFixedFlowCapacityAsync(dto.BranchId, requestedStartAt, requestedEndAt, requestedCluster, dto.SlotHoldToken);
            var created = await PersistBookingAsync(
                externalTransaction,
                actorStaffId,
                dto,
                actualCustomerId,
                bookingGroupId,
                requestedStartAt,
                requestedEndAt,
                flow,
                effectiveRequestedServiceIds,
                requestedCluster);
            if (!string.IsNullOrWhiteSpace(dto.SlotHoldToken))
            {
                _slotHoldService.ConsumeHold(dto.SlotHoldToken);
            }
            return created;
        }


    }
}
