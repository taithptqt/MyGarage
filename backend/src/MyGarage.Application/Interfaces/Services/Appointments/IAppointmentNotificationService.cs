using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IAppointmentNotificationService
    {
        Task TrySendBookingSuccessEmailAsync(long appointmentId);
        Task TrySendBookingSuccessEmailAsync(IReadOnlyCollection<long> appointmentIds);
    }
}
