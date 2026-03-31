using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Constants;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class BookingGroupStatusPolicy : IBookingGroupStatusPolicy
    {
        public BookingGroupStatusEvaluation Evaluate(IReadOnlyCollection<string> statuses, string? previousStatus)
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
                return new BookingGroupStatusEvaluation(
                    BookingGroupStatuses.Completed,
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, BookingGroupStatuses.Completed),
                    false,
                    false,
                    true);
            }

            if (total > 0 && canceled == total)
            {
                return new BookingGroupStatusEvaluation(
                    BookingGroupStatuses.Canceled,
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
                return new BookingGroupStatusEvaluation(
                    BookingGroupStatuses.Confirmed,
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, BookingGroupStatuses.Confirmed),
                    false,
                    false,
                    true);
            }

            if (confirmed > 0 || canceled > 0)
            {
                return new BookingGroupStatusEvaluation(
                    BookingGroupStatuses.PartialConfirmed,
                    total,
                    confirmed,
                    canceled,
                    ShouldSetConfirmedAt(previousStatus, BookingGroupStatuses.PartialConfirmed),
                    false,
                    false,
                    true);
            }

            return new BookingGroupStatusEvaluation(
                BookingGroupStatuses.Pending,
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

            if (string.Equals(previousStatus, BookingGroupStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, BookingGroupStatuses.PartialConfirmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(previousStatus, BookingGroupStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSetCanceledAt(string? previousStatus)
        {
            return !string.Equals(previousStatus, BookingGroupStatuses.Canceled, StringComparison.OrdinalIgnoreCase);
        }
    }
}
