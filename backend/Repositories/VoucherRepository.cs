// Mục đích: Truy cập dữ liệu cho bản ghi voucher chính.
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VoucherRepository : GenericRepository<Voucher>, IVoucherRepository
    {
        public VoucherRepository(IGarageContext context) : base(context)
        {
        }
    }
}

