using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<Appointment> BookAsync(long authUserId, BookAppointmentRequestDto dto)
        {
            return await BookCoreAsync(authUserId, dto, null, null);
        }

        public async Task<BookingGroupResponseDto> BookBatchAsync(long authUserId, BookAppointmentBatchRequestDto dto)
        {
            return await BookBatchForCustomerAsync(authUserId, authUserId, dto);
        }

        public async Task<BookingGroupResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Danh sach lich hen khong duoc de trong.");

            if (targetCustomerId <= 0)
                throw new InvalidOperationException("Khach hang dat lich khong hop le.");

            var fixedLeadViolations = new List<string>();
            foreach (var item in dto.Items)
            {
                var flow = await ResolveFlowAsync(item.BookingFlow, item.RequestedServiceIds);
                var (requestedStartAt, _) = ResolveRequestedWindow(flow, item);
                var opHour = await _context.BranchOperatingHours
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)requestedStartAt.DayOfWeek);
                var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
                var minLeadMinutes = Math.Max(15, slotStepMinutes);
                if (requestedStartAt.Date == _clock.LocalNow.Date && requestedStartAt < _clock.LocalNow.AddMinutes(minLeadMinutes))
                {
                    fixedLeadViolations.Add($"{ResolveBookingItemLabel(item)} (cần cách hiện tại ít nhất {minLeadMinutes} phút)");
                }
            }

            if (fixedLeadViolations.Count > 0)
                throw new InvalidOperationException("Các xe chưa đáp ứng điều kiện giờ cố định: " + string.Join("; ", fixedLeadViolations));

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var actorStaffId = await _bookingGroupService.ResolveStaffActorIdAsync(actorUserId);
                var group = await _bookingGroupService.CreateAsync(targetCustomerId, dto.BranchId, dto.CustomerNote, actorStaffId);

                var createdAppointmentIds = new List<long>();
                foreach (var item in dto.Items)
                {
                    item.BranchId = dto.BranchId;
                    item.CreatedBy = actorStaffId;
                    item.CustomerId ??= targetCustomerId;
                    var created = await BookCoreAsync(actorUserId, item, group.Id, tx);
                    createdAppointmentIds.Add(created.Id);
                }

                await _bookingGroupService.SyncCountersAsync(group.Id, actorStaffId);
                await tx.CommitAsync();

                var response = await _bookingGroupService.GetByCustomerAndIdAsync(targetCustomerId, group.Id);
                if (response != null)
                {
                    response.Appointments = response.Appointments
                        .Where(a => createdAppointmentIds.Contains(a.Id))
                        .ToList();
                    return response;
                }

                throw new InvalidOperationException("Khong the tai thong tin nhom lich hen sau khi tao.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private static string ResolveBookingItemLabel(BookAppointmentRequestDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.LicensePlate))
                return $"Xe {dto.LicensePlate.Trim()}";
            if (dto.VehicleId.HasValue)
                return $"Xe ID {dto.VehicleId.Value}";
            if (!string.IsNullOrWhiteSpace(dto.Vin))
                return $"Xe VIN {dto.Vin.Trim()}";
            return "Xe chưa rõ biển số";
        }

    }
}

