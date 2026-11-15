using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface IAppointmentPreallocationService
    {
        Task<List<Appointment>> LoadProjectedRepairReservationsAsync(
            long branchId,
            DateTime startAt,
            DateTime endAt);

        Task<List<WorkSlot>> BuildFlexibleRepairPreallocatedSlotsAsync(
            Appointment appointment,
            WorkSlot checkInSlot,
            ServiceCluster requestedCluster,
            IReadOnlyList<string> orderedBayTypes,
            string targetBayAreaTag,
            int repairMinutes,
            int slotStepMinutes);
    }
}
