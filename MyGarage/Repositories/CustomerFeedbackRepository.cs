using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class CustomerFeedbackRepository : GenericRepository<CustomerFeedback>, ICustomerFeedbackRepository
    {
        public CustomerFeedbackRepository(GarageContext context) : base(context)
        {
        }
    }
}
