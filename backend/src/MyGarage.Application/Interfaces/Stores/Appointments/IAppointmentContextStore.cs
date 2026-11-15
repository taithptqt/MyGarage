namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentContextStore :
    IAppointmentCustomerVehicleStore,
    IAppointmentInsuranceStore,
    IAppointmentOperationalContextStore
{
}
