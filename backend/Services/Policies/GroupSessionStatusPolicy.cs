using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Constants;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class GroupSessionStatusPolicy : IGroupSessionStatusPolicy
    {
        public GroupSessionStatusEvaluation Evaluate(IReadOnlyCollection<string> statuses, string? previousStatus)
        {
            var total = statuses.Count;
            var canceled = statuses.Count(x =>
                string.Equals(x, nameof(AppointmentStatus.CANCELED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, nameof(AppointmentStatus.NO_SHOW), StringComparison.OrdinalIgnoreCase));
            var confirmed = statuses.Count(x =>
                string.Equals(x, nameof(AppointmentStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, nameof(AppointmentStatus.ARRIVED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, nameof(AppointmentStatus.ARRIVED_LATE), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, nameof(AppointmentStatus.COMPLETED), StringComparison.OrdinalIgnoreCase));
            var completed = statuses.Count(x => string.Equals(x, nameof(AppointmentStatus.COMPLETED), StringComparison.OrdinalIgnoreCase));

            if (total > 0 && completed == total)
            {
                return new GroupSessionStatusEvaluation(
                    nameof(BookingGroupStatus.COMPLETED),
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, nameof(BookingGroupStatus.COMPLETED)),
                    false,
                    false,
                    true);
            }

            if (total > 0 && canceled == total)
            {
                return new GroupSessionStatusEvaluation(
                    nameof(BookingGroupStatus.CANCELED),
                    total,
                    confirmed,
                    canceled,
                    false,
                    ShouldSetCanceledAt(previousStatus),
                    true,
                    false);
            }

            if (total > 0 && confirmed == total)
            {
                return new GroupSessionStatusEvaluation(
                    nameof(BookingGroupStatus.CONFIRMED),
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, nameof(BookingGroupStatus.CONFIRMED)),
                    false,
                    false,
                    true);
            }

            if (confirmed > 0 || canceled > 0)
            {
                return new GroupSessionStatusEvaluation(
                    nameof(BookingGroupStatus.PARTIAL_CONFIRMED),
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, nameof(BookingGroupStatus.PARTIAL_CONFIRMED)),
                    false,
                    false,
                    true);
            }

            return new GroupSessionStatusEvaluation(
                nameof(BookingGroupStatus.PENDING),
                total,
                confirmed,
                canceled,
                false,
                false,
                true,
                true);
        }

        private static bool ShouldSetConfirmedAt(string? previousStatus, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(previousStatus))
            {
                return true;
            }

            if (string.Equals(previousStatus, nameof(BookingGroupStatus.CONFIRMED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, nameof(BookingGroupStatus.PARTIAL_CONFIRMED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, nameof(BookingGroupStatus.COMPLETED), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSetCanceledAt(string? previousStatus)
        {
            return !string.Equals(previousStatus, nameof(BookingGroupStatus.CANCELED), StringComparison.OrdinalIgnoreCase);
        }
    }
}
