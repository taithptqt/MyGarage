using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VoucherService : GenericService<Voucher>, IVoucherService
    {
        public VoucherService(IVoucherRepository repository) : base(repository)
        {
        }
    }
}
