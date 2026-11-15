using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Bays;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class BayManagementService : IBayManagementService
    {
        private readonly IBayManagementStore _store;
        private readonly IBayRealtimeNotifier _realtimeNotifier;

        public BayManagementService(IBayManagementStore store, IBayRealtimeNotifier realtimeNotifier)
        {
            _store = store;
            _realtimeNotifier = realtimeNotifier;
        }

        public async Task<BayAllocationDto> AllocateBayAsync(BayAllocationCreateDto model, long staffId)
        {
            var bay = await _store.GetBayByIdAsync(model.BayId);
            if (bay == null) throw new NotFoundException("Bay not found");

            if (bay.CurrentOccupiedVehicleId.HasValue && bay.OperationalStatus == "InUse")
            {
                throw new BadRequestException("Bay is already occupied");
            }

            var serviceOrderExists = await _store.ServiceOrderExistsAsync(model.ServiceOrderId);
            if (!serviceOrderExists) throw new NotFoundException("Service order not found");

            var vehicle = await _store.GetVehicleByIdAsync(model.VehicleId);
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

            await _store.AddBayAllocationAsync(allocation);

            var statusLog = new BayStatusLog
            {
                BayId = model.BayId,
                OldStatus = bay.OperationalStatus ?? "Available",
                NewStatus = "InUse",
                Reason = $"Allocated to vehicle {vehicle.LicensePlate}",
                ChangedBy = staffId,
                ChangedAt = now
            };

            await _store.AddBayStatusLogAsync(statusLog);

            bay.OperationalStatus = "InUse";
            bay.CurrentOccupiedVehicleId = model.VehicleId;
            bay.LastStatusChangedAt = now;

            await _store.SaveChangesInTransactionAsync();
            await _realtimeNotifier.BroadcastBayTimelineUpdatedAsync(bay.BranchId);

            var result = await _store.GetBayAllocationWithDetailsAsync(allocation.Id);

            return MapToDto(result);
        }

        public async Task<BayAllocationDto> ReleaseBayAsync(long bayId, long releaseByStaffId, string? notes)
        {
            var bay = await _store.GetBayByIdAsync(bayId);
            if (bay == null) throw new NotFoundException("Bay not found");

            var allocation = await _store.GetActiveBayAllocationAsync(bayId);

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

            await _store.AddBayStatusLogAsync(statusLog);

            bay.OperationalStatus = "Available";
            bay.CurrentOccupiedVehicleId = null;
            bay.LastStatusChangedAt = now;

            await _store.SaveChangesInTransactionAsync();
            await _realtimeNotifier.BroadcastBayTimelineUpdatedAsync(bay.BranchId);

            var result = await _store.GetBayAllocationWithDetailsAsync(allocation.Id);

            return MapToDto(result);
        }

        public async Task<IEnumerable<BayTimelineDto>> GetBayTimelineAsync(DateTime date, long branchId)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var bays = await _store.GetActiveBaysByBranchAsync(branchId);
            var allocations = await _store.GetBayAllocationsForTimelineAsync(branchId, startOfDay, endOfDay);

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
                ServiceOrderCode = a.ServiceOrder?.Code ?? string.Empty,
                VehicleId = a.VehicleId,
                LicensePlate = a.Vehicle?.LicensePlate ?? string.Empty,
                AllocatedAt = a.AllocatedAt,
                AllocatedBy = a.AllocatedBy,
                AllocatedByName = a.AllocatedByStaff?.FullName ?? string.Empty,
                ReleasedAt = a.ReleasedAt,
                ReleasedBy = a.ReleasedBy,
                ReleasedByName = a.ReleasedByStaff?.FullName,
                PlannedDuration = a.PlannedDuration,
                ActualDuration = actualDuration,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            };
        }
    }
}
