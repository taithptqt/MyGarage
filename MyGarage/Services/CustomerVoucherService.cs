using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerVoucherService : GenericService<CustomerVoucher>, ICustomerVoucherService
    {
        public CustomerVoucherService(ICustomerVoucherRepository repository) : base(repository)
        {
        }
    }
}
