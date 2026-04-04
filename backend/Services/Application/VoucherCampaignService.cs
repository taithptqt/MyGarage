// Mục đích: Xử lý nghiệp vụ chiến dịch voucher như validate dữ liệu, vòng đời chiến dịch và gán voucher vào chiến dịch.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherCampaignService : GenericService<VoucherCampaign>, IVoucherCampaignService
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(VoucherCampaignStatusCode.DRAFT),
            nameof(VoucherCampaignStatusCode.PENDING_APPROVAL),
            nameof(VoucherCampaignStatusCode.SCHEDULED),
            nameof(VoucherCampaignStatusCode.ACTIVE),
            nameof(VoucherCampaignStatusCode.PAUSED),
            nameof(VoucherCampaignStatusCode.ENDED),
            nameof(VoucherCampaignStatusCode.ARCHIVED)
        };

        private readonly IVoucherCampaignRepository _voucherCampaignRepository;
        private readonly IGarageContext _context;

        public VoucherCampaignService(IVoucherCampaignRepository repository, IGarageContext context)
            : base(repository)
        {
            _voucherCampaignRepository = repository;
            _context = context;
        }

    }
}


