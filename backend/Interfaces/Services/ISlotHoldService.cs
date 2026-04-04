using System;
using System.Collections.Generic;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface ISlotHoldService
    {
        SlotHoldRecord CreateHold(long branchId, DateTime startTime, DateTime endTime, string bookingFlow, string? requestedServiceIds, int vehicleCount);
        bool ReleaseHold(string token);
        bool TryGetActiveHold(string token, out SlotHoldRecord? hold);
        bool ConsumeHold(string token);
        IReadOnlyList<SlotHoldRecord> GetActiveHolds(long branchId, DateTime startTime, DateTime endTime, string bookingFlow, string? excludeToken = null);
    }
}

