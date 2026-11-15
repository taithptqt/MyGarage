using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService : GenericService<Appointment>, IAppointmentService
    {
        private readonly IAppointmentAvailabilityService _availabilityService;
        private readonly IAppointmentLifecycleService _lifecycleService;
        private readonly IAppointmentBookingService _bookingService;
        private readonly IAppointmentQueryService _queryService;

        public AppointmentService(
            IAppointmentRepository repository,
            IAppointmentAvailabilityService availabilityService,
            IAppointmentLifecycleService lifecycleService,
            IAppointmentBookingService bookingService,
            IAppointmentQueryService queryService)
            : base(repository)
        {
            _availabilityService = availabilityService;
            _lifecycleService = lifecycleService;
            _bookingService = bookingService;
            _queryService = queryService;
        }
    }
}
