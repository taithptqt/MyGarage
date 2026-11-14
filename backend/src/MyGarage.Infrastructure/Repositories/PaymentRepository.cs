using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Payment>> GetByCustomerPayerAsync(long customerId)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p =>
                    p.PayerId == customerId &&
                    p.PayerType != null &&
                    p.PayerType.ToUpper() == "CUSTOMER")
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}

