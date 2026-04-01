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
            "DRAFT",
            "PENDING_APPROVAL",
            "SCHEDULED",
            "ACTIVE",
            "PAUSED",
            "ENDED",
            "ARCHIVED"
        };

        private readonly IVoucherCampaignRepository _voucherCampaignRepository;
        private readonly GarageContext _context;

        public VoucherCampaignService(IVoucherCampaignRepository repository, GarageContext context)
            : base(repository)
        {
            _voucherCampaignRepository = repository;
            _context = context;
        }

    }
}

