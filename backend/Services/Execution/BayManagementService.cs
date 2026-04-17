using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class BayManagementService : IBayManagementService
    {
        private readonly IGarageContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public BayManagementService(IGarageContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<BayAllocationDto> AllocateBayAsync(BayAllocationCreateDto model, long staffId)
        {
            var bay = await _context.Bays.FindAsync(model.BayId);
            if (bay == null) throw new NotFoundException("Bay not found");

            if (bay.CurrentOccupiedVehicleId.HasValue && bay.OperationalStatus == "InUse")
            {
                throw new BadRequestException("Bay is already occupied");
            }

            var serviceOrder = await _context.ServiceOrders.FindAsync(model.ServiceOrderId);
            if (serviceOrder == null) throw new NotFoundException("Service order not found");

            var vehicle = await _context.Vehicles.FindAsync(model.VehicleId);
            if (vehicle == null) throw new NotFoundException("Vehicle not found");

            var now = VietnamTime.Now;
            var allocation = new BayAllocation
            {
                BayId = model.BayId,
                ServiceOrderId = model.ServiceOrderId,
                VehicleId = model.VehicleId,
                AllocatedAt = now,
                AllocatedBy = staffId,
                PlannedDuration = model.PlannedDuration,
                Notes = model.Notes
            };

            await _context.BayAllocations.AddAsync(allocation);

            var statusLog = new BayStatusLog
            {
                BayId = model.BayId,
                OldStatus = bay.OperationalStatus ?? "Available",
                NewStatus = "InUse",
                Reason = $"Allocated to vehicle {vehicle.LicensePlate}",
                ChangedBy = staffId,
                ChangedAt = now
            };
            
            await _context.BayStatusLogs.AddAsync(statusLog);

            bay.OperationalStatus = "InUse";
            bay.CurrentOccupiedVehicleId = model.VehicleId;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync();
            await BroadcastBayTimelineUpdatedAsync(bay.BranchId);

            var result = await _context.BayAllocations.AsNoTracking()
                .Include(a => a.Bay)
                .Include(a => a.ServiceOrder)
                .Include(a => a.Vehicle)
                .Include(a => a.AllocatedByStaff)
                .FirstAsync(a => a.Id == allocation.Id);

            return MapToDto(result);
        }

        public async Task<BayAllocationDto> ReleaseBayAsync(long bayId, long releaseByStaffId, string? notes)
        {
            var bay = await _context.Bays.FindAsync(bayId);
            if (bay == null) throw new NotFoundException("Bay not found");

            var allocation = await _context.BayAllocations
                .Where(a => a.BayId == bayId && a.ReleasedAt == null)
                .OrderByDescending(a => a.AllocatedAt)
                .FirstOrDefaultAsync();

            if (allocation == null) throw new BadRequestException("No active allocation found for this bay");

            var now = VietnamTime.Now;
            allocation.ReleasedAt = now;
            allocation.ReleasedBy = releaseByStaffId;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                allocation.Notes = string.IsNullOrEmpty(allocation.Notes) ? notes : $"{allocation.Notes}\nRelease notes: {notes}";
            }

            var statusLog = new BayStatusLog
            {
                BayId = bayId,
                OldStatus = bay.OperationalStatus ?? "InUse",
                NewStatus = "Available",
                Reason = "Vehicle released",
                ChangedBy = releaseByStaffId,
                ChangedAt = now
            };
            
            await _context.BayStatusLogs.AddAsync(statusLog);

            bay.OperationalStatus = "Available";
            bay.CurrentOccupiedVehicleId = null;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync();
            await BroadcastBayTimelineUpdatedAsync(bay.BranchId);

            var result = await _context.BayAllocations.AsNoTracking()
                .Include(a => a.Bay)
                .Include(a => a.ServiceOrder)
                .Include(a => a.Vehicle)
                .Include(a => a.AllocatedByStaff)
                .Include(a => a.ReleasedByStaff)
                .FirstAsync(a => a.Id == allocation.Id);

            return MapToDto(result);
        }

        public async Task<IEnumerable<BayTimelineDto>> GetBayTimelineAsync(DateTime date, long branchId)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var bays = await _context.Bays
                .Where(b => b.BranchId == branchId && b.IsDeleted == false)
                .ToListAsync();

            var allocations = await _context.BayAllocations
                .Include(a => a.Bay)
                .Include(a => a.ServiceOrder)
                .Include(a => a.Vehicle)
                .Include(a => a.AllocatedByStaff)
                .Include(a => a.ReleasedByStaff)
                .Where(a => a.Bay.BranchId == branchId && 
                            a.AllocatedAt < endOfDay && 
                            (a.ReleasedAt == null || a.ReleasedAt >= startOfDay))
                .ToListAsync();

            var timeline = new List<BayTimelineDto>();

            foreach (var bay in bays)
            {
                var bayAllocations = allocations.Where(a => a.BayId == bay.Id).Select(MapToDto).ToList();
                
                timeline.Add(new BayTimelineDto
                {
                    BayId = bay.Id,
                    BayName = bay.Name,
                    ServiceArea = bay.ServiceArea ?? string.Empty,
                    OperationalStatus = bay.OperationalStatus ?? "Available",
                    Allocations = bayAllocations
                });
            }

            return timeline;
        }

        private static BayAllocationDto MapToDto(BayAllocation a)
        {
            var now = VietnamTime.Now;
            var actualDuration = a.ReleasedAt.HasValue
                ? (int)(a.ReleasedAt.Value - a.AllocatedAt).TotalMinutes 
                : (int)(now - a.AllocatedAt).TotalMinutes;

            return new BayAllocationDto
            {
                Id = a.Id,
                BayId = a.BayId,
                BayName = a.Bay?.Name ?? string.Empty,
                ServiceOrderId = a.ServiceOrderId,
                ServiceOrderCode = a.ServiceOrder?.Code ?? string.Empty, // Assuming ServiceOrder has OrderNumber or similar. Adjust if needed.
                VehicleId = a.VehicleId,
                LicensePlate = a.Vehicle?.LicensePlate ?? string.Empty,
                AllocatedAt = a.AllocatedAt,
                AllocatedBy = a.AllocatedBy,
                AllocatedByName = a.AllocatedByStaff?.FullName ?? string.Empty, // Assuming Staff has FullName
                ReleasedAt = a.ReleasedAt,
                ReleasedBy = a.ReleasedBy,
                ReleasedByName = a.ReleasedByStaff?.FullName,
                PlannedDuration = a.PlannedDuration,
                ActualDuration = actualDuration,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            };
        }

        private async Task BroadcastBayTimelineUpdatedAsync(long branchId)
        {
            if (branchId <= 0)
            {
                return;
            }

            await _hubContext.Clients
                .Group($"WorkshopBranch_{branchId}")
                .SendAsync("BayTimelineUpdated", new
                {
                    branchId,
                    changedAt = VietnamTime.Now
                });
        }
    }
}
