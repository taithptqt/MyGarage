using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class SlotHoldService : ISlotHoldService
    {
        private const int HoldMinutes = 15;
        private readonly ConcurrentDictionary<string, SlotHoldRecord> _holds = new();

        public SlotHoldRecord CreateHold(
            long branchId,
            DateTime startTime,
            DateTime endTime,
            string bookingFlow,
            string? requestedServiceIds,
            string? requestedPackageIds,
            int vehicleCount)
        {
            PurgeExpired(VietnamTime.Now);
            var now = VietnamTime.Now;
            var token = Guid.NewGuid().ToString("N");
            var record = new SlotHoldRecord
            {
                Token = token,
                BranchId = branchId,
                StartTime = startTime,
                EndTime = endTime,
                BookingFlow = string.IsNullOrWhiteSpace(bookingFlow) ? "FIXED" : bookingFlow.Trim().ToUpperInvariant(),
                RequestedServiceIds = requestedServiceIds,
                RequestedPackageIds = requestedPackageIds,
                VehicleCount = Math.Max(1, vehicleCount),
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(HoldMinutes)
            };
            _holds[token] = record;
            return record;
        }

        public bool ReleaseHold(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return _holds.TryRemove(token.Trim(), out _);
        }

        public bool TryGetActiveHold(string token, out SlotHoldRecord? hold)
        {
            hold = null;
            if (string.IsNullOrWhiteSpace(token)) return false;

            PurgeExpired(VietnamTime.Now);
            if (!_holds.TryGetValue(token.Trim(), out var found))
            {
                return false;
            }

            hold = found;
            return true;
        }

        public bool ConsumeHold(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            PurgeExpired(VietnamTime.Now);
            return _holds.TryRemove(token.Trim(), out _);
        }

        public IReadOnlyList<SlotHoldRecord> GetActiveHolds(
            long branchId,
            DateTime startTime,
            DateTime endTime,
            string bookingFlow,
            string? excludeToken = null)
        {
            PurgeExpired(VietnamTime.Now);
            var flow = string.IsNullOrWhiteSpace(bookingFlow) ? "FIXED" : bookingFlow.Trim().ToUpperInvariant();
            var excluded = string.IsNullOrWhiteSpace(excludeToken) ? null : excludeToken.Trim();
            return _holds.Values
                .Where(h =>
                    h.BranchId == branchId &&
                    h.BookingFlow == flow &&
                    (excluded == null || !string.Equals(h.Token, excluded, StringComparison.Ordinal)) &&
                    h.StartTime < endTime &&
                    h.EndTime > startTime)
                .ToList();
        }

        private void PurgeExpired(DateTime now)
        {
            foreach (var item in _holds)
            {
                if (item.Value.ExpiresAt <= now)
                {
                    _holds.TryRemove(item.Key, out _);
                }
            }
        }
    }
}

