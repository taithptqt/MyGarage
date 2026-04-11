using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPaymentService : IGenericService<Payment>
    {
        Task<IEnumerable<Payment>> GetByCustomerPayerAsync(long customerId);
    }
}
