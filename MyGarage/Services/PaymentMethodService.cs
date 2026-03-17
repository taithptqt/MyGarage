using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PaymentMethodService : GenericService<PaymentMethod>, IPaymentMethodService
    {
        public PaymentMethodService(IPaymentMethodRepository repository) : base(repository)
        {
        }
    }
}
