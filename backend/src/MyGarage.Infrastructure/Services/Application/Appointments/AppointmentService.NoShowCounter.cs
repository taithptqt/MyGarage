using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task IncrementNoShowCountAsync(long customerId, int delta = 1)
        {
            if (customerId <= 0 || delta <= 0)
            {
                return;
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == customerId);
            if (customer == null)
            {
                return;
            }

            customer.NoShowCount = (customer.NoShowCount ?? 0) + delta;
        }

        private async Task IncrementNoShowCountsAsync(IReadOnlyDictionary<long, int> customerDeltas)
        {
            if (customerDeltas.Count == 0)
            {
                return;
            }

            var customerIds = customerDeltas.Keys.Where(x => x > 0).Distinct().ToList();
            if (customerIds.Count == 0)
            {
                return;
            }

            var customers = await _context.Customers
                .Where(x => customerIds.Contains(x.Id))
                .ToListAsync();

            foreach (var customer in customers)
            {
                if (!customerDeltas.TryGetValue(customer.Id, out var delta) || delta <= 0)
                {
                    continue;
                }

                customer.NoShowCount = (customer.NoShowCount ?? 0) + delta;
            }
        }

        private static void AddNoShowDelta(Dictionary<long, int> customerDeltas, long customerId)
        {
            if (customerId <= 0)
            {
                return;
            }

            if (customerDeltas.TryGetValue(customerId, out var current))
            {
                customerDeltas[customerId] = current + 1;
                return;
            }

            customerDeltas[customerId] = 1;
        }
    }
}
