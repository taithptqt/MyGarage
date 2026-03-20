using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ServiceStaffAssignmentRepository : GenericRepository<ServiceStaffAssignment>, IServiceStaffAssignmentRepository
    {
        public ServiceStaffAssignmentRepository(GarageContext context) : base(context)
        {
        }
    }
}
