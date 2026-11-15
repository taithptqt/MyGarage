// Mục đích: Kiểm tra điều kiện áp dụng voucher và xử lý giao dịch redeem/reverse.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService : GenericService<VoucherRedemption>, IVoucherRedemptionService
    {
        private readonly IVoucherRedemptionRepository _voucherRedemptionRepository;
        private readonly IGarageContext _context;

        public VoucherRedemptionService(IVoucherRedemptionRepository repository, IGarageContext context)
            : base(repository)
        {
            _voucherRedemptionRepository = repository;
            _context = context;
        }

        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync();
        }

        // All voucher writers in this service acquire the same transaction-owned SQL lock.
        // Rowversion remains a second guard against writers outside this service.
        private async Task AcquireVoucherOperationLockAsync(string name)
        {
            if (!_context.Database.IsSqlServer()) return;
            var resource = "MyGarage:Voucher:" + name;
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
    @LockOwner='Transaction', @LockTimeout=10000;
IF @result < 0 THROW 51001, 'Could not acquire voucher operation lock.', 1;");
        }
    }
}

