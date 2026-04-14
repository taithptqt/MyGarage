using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PaymentService : GenericService<Payment>, IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;

        public PaymentService(IPaymentRepository repository) : base(repository)
        {
            _paymentRepository = repository;
        }

        public Task<IEnumerable<Payment>> GetByCustomerPayerAsync(long customerId)
            => _paymentRepository.GetByCustomerPayerAsync(customerId);
    }
}
