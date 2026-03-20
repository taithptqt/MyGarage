using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AppointmentService : GenericService<Appointment>, IAppointmentService
    {
        public AppointmentService(IAppointmentRepository repository) : base(repository)
        {
        }
    }
}
