using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PaymentService : GenericService<Payment>, IPaymentService
    {
        public PaymentService(IPaymentRepository repository) : base(repository)
        {
        }
    }
}
