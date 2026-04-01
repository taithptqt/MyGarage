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
            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Danh sach lich hen khong duoc de trong.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var actorStaffId = await _bookingGroupService.ResolveStaffActorIdAsync(authUserId);
                var group = await _bookingGroupService.CreateAsync(authUserId, dto.BranchId, dto.CustomerNote, actorStaffId);

                var createdAppointmentIds = new List<long>();
                foreach (var item in dto.Items)
                {
                    item.BranchId = dto.BranchId;
                    item.CreatedBy = actorStaffId;
                    var created = await BookCoreAsync(authUserId, item, group.Id, tx);
                    createdAppointmentIds.Add(created.Id);
                }

                await _bookingGroupService.SyncCountersAsync(group.Id, actorStaffId);
                await tx.CommitAsync();

                var response = await _bookingGroupService.GetByCustomerAndIdAsync(authUserId, group.Id);
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

    }
}

