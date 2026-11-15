namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentScheduleStore :
    IAppointmentBranchCapacityStore,
    IAppointmentScheduleQueryStore,
    IAppointmentWorkSlotStore
{
}
