// Mục đích: Kiểm tra điều kiện áp dụng voucher và xử lý giao dịch redeem/reverse.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

    }
}


