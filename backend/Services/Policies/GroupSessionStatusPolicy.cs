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
                string.Equals(x, AppointmentStatuses.Canceled, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, AppointmentStatuses.NoShow, StringComparison.OrdinalIgnoreCase));
            var confirmed = statuses.Count(x =>
                string.Equals(x, AppointmentStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, AppointmentStatuses.Arrived, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, AppointmentStatuses.ArrivedLate, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, AppointmentStatuses.Completed, StringComparison.OrdinalIgnoreCase));
            var completed = statuses.Count(x => string.Equals(x, AppointmentStatuses.Completed, StringComparison.OrdinalIgnoreCase));

            if (total > 0 && completed == total)
            {
                return new GroupSessionStatusEvaluation(
                    GroupSessionStatuses.Completed,
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, GroupSessionStatuses.Completed),
                    false,
                    false,
                    true);
            }

            if (total > 0 && canceled == total)
            {
                return new GroupSessionStatusEvaluation(
                    GroupSessionStatuses.Canceled,
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
                    GroupSessionStatuses.Confirmed,
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, GroupSessionStatuses.Confirmed),
                    false,
                    false,
                    true);
            }

            if (confirmed > 0 || canceled > 0)
            {
                return new GroupSessionStatusEvaluation(
                    GroupSessionStatuses.PartialConfirmed,
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, GroupSessionStatuses.PartialConfirmed),
                    false,
                    false,
                    true);
            }

            return new GroupSessionStatusEvaluation(
                GroupSessionStatuses.Pending,
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

            if (string.Equals(previousStatus, GroupSessionStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, GroupSessionStatuses.PartialConfirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, GroupSessionStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSetCanceledAt(string? previousStatus)
        {
            return !string.Equals(previousStatus, GroupSessionStatuses.Canceled, StringComparison.OrdinalIgnoreCase);
        }
    }
}
