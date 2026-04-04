using System.Collections.Generic;

namespace MyGarage.Constants
{
    public static class AppointmentStatusGroups
    {
        public static readonly HashSet<string> Closed = new(System.StringComparer.OrdinalIgnoreCase)
        {
            nameof(AppointmentStatus.CANCELED),
            nameof(AppointmentStatus.CANCELLED),
            nameof(AppointmentStatus.NO_SHOW),
            nameof(AppointmentStatus.COMPLETED),
        };

        public static readonly HashSet<string> Queueing = new(System.StringComparer.OrdinalIgnoreCase)
        {
            nameof(AppointmentStatus.PENDING),
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE),
        };
    }
}

