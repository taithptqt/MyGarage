using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceStaffAssignmentService : GenericService<ServiceStaffAssignment>, IServiceStaffAssignmentService
    {
        public ServiceStaffAssignmentService(IServiceStaffAssignmentRepository repository) : base(repository)
        {
        }
    }
}
