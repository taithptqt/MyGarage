namespace MyGarage.Interfaces
{
    public interface IAppointmentAvailabilityService :
        IAppointmentPackageResolver,
        IAppointmentBookingFlowResolver,
        IAppointmentSlotHoldOperations,
        IAppointmentAvailabilityQuery,
        IAppointmentSchedulingAvailability
    {
    }
}
