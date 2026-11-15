using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentService :
        IGenericService<Appointment>,
        IAppointmentReadService,
        IAppointmentAvailabilityQueryService,
        IAppointmentSlotHoldService,
        IAppointmentBookingCommandService,
        IAppointmentLifecycleCommandService,
        IAppointmentDispatchOpsService
    {
    }
}
