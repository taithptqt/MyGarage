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
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<Appointment> BookAsync(long authUserId, BookAppointmentRequestDto dto)
        {
            return await BookCoreAsync(authUserId, dto, null, null);
        }

        public async Task<GroupSessionResponseDto> BookBatchAsync(long authUserId, BookAppointmentBatchRequestDto dto)
        {
            return await BookBatchForCustomerAsync(authUserId, authUserId, dto);
        }

        public async Task<GroupSessionResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Danh sách lịch hẹn không được để trống.");

            if (targetCustomerId <= 0)
                throw new InvalidOperationException("Khách hàng đặt lịch không hợp lệ.");

            var fixedLeadViolations = new List<string>();
            foreach (var item in dto.Items)
            {
                var flow = await ResolveFlowAsync(item.BookingFlow, item.RequestedServiceIds, item.RequestedPackageIds);
                var (requestedStartAt, _) = ResolveRequestedWindow(flow, item);
                var opHour = await _context.BranchOperatingHours
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)requestedStartAt.DayOfWeek);
                var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
                var minLeadMinutes = Math.Max(15, slotStepMinutes);
                if (requestedStartAt.Date == _clock.LocalNow.Date && requestedStartAt < _clock.LocalNow.AddMinutes(minLeadMinutes))
                {
                    fixedLeadViolations.Add($"{ResolveBookingItemLabel(item)} (c?n c�ch hi?n t?i �t nh?t {minLeadMinutes} ph�t)");
                }
            }

            if (fixedLeadViolations.Count > 0)
                throw new InvalidOperationException("C�c xe chua d�p ?ng di?u ki?n gi? c? d?nh: " + string.Join("; ", fixedLeadViolations));

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
                var group = await _groupSessionService.CreateAsync(targetCustomerId, dto.BranchId, dto.CustomerNote, actorStaffId);

                var createdAppointmentIds = new List<long>();
                foreach (var item in dto.Items)
                {
                    item.BranchId = dto.BranchId;
                    item.CreatedBy = actorStaffId;
                    item.CustomerId ??= targetCustomerId;
                    var created = await BookCoreAsync(actorUserId, item, group.Id, tx);
                    createdAppointmentIds.Add(created.Id);
                }

                await _groupSessionService.SyncCountersAsync(group.Id, actorStaffId);
                await tx.CommitAsync();

                var response = await _groupSessionService.GetByCustomerAndIdAsync(targetCustomerId, group.Id);
                if (response != null)
                {
                    var createdAppointments = await _context.Appointments
                        .AsNoTracking()
                        .Include(a => a.Branch)
                        .Include(a => a.Customer)
                        .Include(a => a.GroupSession)
                        .Include(a => a.Vehicle)
                            .ThenInclude(v => v!.Model)
                                .ThenInclude(m => m.Brand)
                        .Where(a => createdAppointmentIds.Contains(a.Id))
                        .OrderBy(a => a.StartTime)
                        .ToListAsync();

                    response.Appointments = (await _appointmentDetailAssembler.AssembleManyAsync(createdAppointments))
                        .ToList();
                    return response;
                }

                throw new InvalidOperationException("Không thể tải thông tin nhóm lịch hẹn sau khi tạo.");
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
            return "Xe chua r� bi?n s?";
        }

    }
}

