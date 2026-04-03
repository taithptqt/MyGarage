using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null)
        {
            var snapshot = at ?? _clock.LocalNow;
            var bays = await _context.Bays.AsNoTracking().Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true).OrderBy(b => b.Code).ToListAsync();
            var slots = await _context.WorkSlots.AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.StartTime <= snapshot
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > snapshot)
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != AppointmentStatuses.CancelledLegacy)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            var bayDtos = new List<DispatchBayStatusDto>();
            var busy = 0;
            var overflow = 0;
            foreach (var bay in bays)
            {
                var slot = slots.FirstOrDefault(s => s.BayId == bay.Id);
                if (slot != null) busy++;
                var isOverflow = slot != null && IsRepairBay(bay) && IsQuickSlotType(slot.SlotType, slot.Notes);
                if (isOverflow) overflow++;

                bayDtos.Add(new DispatchBayStatusDto
                {
                    BayId = bay.Id,
                    BayCode = bay.Code,
                    BayName = bay.Name,
                    BayType = bay.BayType,
                    ServiceArea = bay.ServiceArea ?? string.Empty,
                    Status = slot == null ? "IDLE" : "BUSY",
                    IsOverflowFromQuick = isOverflow,
                    SlotType = slot?.SlotType,
                    CurrentCustomerId = slot?.CustomerId,
                    CurrentVehicleId = slot?.VehicleId,
                    PlannedReleaseAt = slot?.EndTime,
                    ActualReleaseAt = slot?.ActualReleaseTime
                });
            }

            var queue = await _context.Appointments.AsNoTracking().Where(a => a.BranchId == branchId && (a.Status == AppointmentStatuses.Queued || a.Status == AppointmentStatuses.Arrived || a.Status == AppointmentStatuses.ArrivedLate)).CountAsync();
            return new DispatchDashboardResponseDto
            {
                BranchId = branchId,
                SnapshotAt = snapshot,
                TotalBays = bays.Count,
                BusyBays = busy,
                IdleBays = bays.Count - busy,
                OverflowBays = overflow,
                WaitingQueueCount = queue,
                Bays = bayDtos
            };
        }
    }
}
