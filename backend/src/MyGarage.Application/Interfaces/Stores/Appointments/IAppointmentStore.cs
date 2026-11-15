namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentStore :
    IAppointmentCommandStore,
    IAppointmentScheduleStore,
    IAppointmentCatalogStore,
    IAppointmentContextStore
{
}
