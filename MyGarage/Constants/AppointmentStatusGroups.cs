using System.Collections.Generic;

namespace MyGarage.Constants
{
    public static class AppointmentStatusGroups
    {
        public static readonly HashSet<string> Closed = new(System.StringComparer.OrdinalIgnoreCase)
        {
            AppointmentStatuses.Canceled,
            AppointmentStatuses.CancelledLegacy,
            AppointmentStatuses.NoShow,
            AppointmentStatuses.Completed,
        };

        public static readonly HashSet<string> Queueing = new(System.StringComparer.OrdinalIgnoreCase)
        {
            AppointmentStatuses.Queued,
            AppointmentStatuses.Arrived,
            AppointmentStatuses.ArrivedLate,
        };
    }
}
