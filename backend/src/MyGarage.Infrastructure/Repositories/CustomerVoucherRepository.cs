// Mục đích: Truy cập dữ liệu cho bản ghi quan hệ khách hàng - voucher.
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class CustomerVoucherRepository : GenericRepository<CustomerVoucher>, ICustomerVoucherRepository
    {
        public CustomerVoucherRepository(IGarageContext context) : base(context)
        {
        }
    }
}

