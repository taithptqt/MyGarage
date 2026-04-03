using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerFeedbackService : GenericService<CustomerFeedback>, ICustomerFeedbackService
    {
        public CustomerFeedbackService(ICustomerFeedbackRepository repository) : base(repository)
        {
        }
    }
}
