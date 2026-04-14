using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ServiceOrderDetailService : GenericService<ServiceOrderDetail>, IServiceOrderDetailService
    {
        private readonly IGarageContext _context;

        public ServiceOrderDetailService(IServiceOrderDetailRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public override async Task<ServiceOrderDetail> AddAsync(ServiceOrderDetail entity)
        {
            await NormalizeAndValidateInsuranceAsync(entity);
            return await base.AddAsync(entity);
        }

        public override async Task<ServiceOrderDetail?> UpdateAsync(object id, ServiceOrderDetail entity)
        {
            await NormalizeAndValidateInsuranceAsync(entity);
            return await base.UpdateAsync(id, entity);
        }

        private async Task NormalizeAndValidateInsuranceAsync(ServiceOrderDetail entity)
        {
            if (entity == null)
            {
                throw new InvalidOperationException("Dữ liệu chi tiết lệnh sửa chữa không hợp lệ.");
            }

            entity.PayerType = InsuranceBillingRules.NormalizePayerType(entity.PayerType);

            RecomputeFinancialAmounts(entity);

            if (entity.PayerType == InsuranceBillingRules.PayerTypeInsurance)
            {
                if (!entity.InsuranceCompanyId.HasValue || entity.InsuranceCompanyId.Value <= 0)
                {
                    throw new InvalidOperationException("Chi tiết bảo hiểm bắt buộc có InsuranceCompanyId hợp lệ.");
                }

                var companyExists = await _context.InsuranceCompanies
                    .AsNoTracking()
                    .AnyAsync(company => company.Id == entity.InsuranceCompanyId.Value && company.IsDeleted != true);
                if (!companyExists)
                {
                    throw new InvalidOperationException("Không tìm thấy công ty bảo hiểm đã chọn.");
                }

                entity.InsuranceStatus = InsuranceBillingRules.NormalizeInsuranceStatus(entity.InsuranceStatus, entity.PayerType);
                entity.InsuranceRequestedAmount = NormalizeNullableMoney(entity.InsuranceRequestedAmount, entity.FinalAmount, nameof(entity.InsuranceRequestedAmount));
                entity.InsuranceApprovedAmount = NormalizeNullableMoney(entity.InsuranceApprovedAmount, 0m, nameof(entity.InsuranceApprovedAmount));
                entity.CustomerCoPayAmount = NormalizeNullableMoney(entity.CustomerCoPayAmount, 0m, nameof(entity.CustomerCoPayAmount));
                entity.GarageAbsorbedAmount = NormalizeNullableMoney(entity.GarageAbsorbedAmount, 0m, nameof(entity.GarageAbsorbedAmount));
                return;
            }

            entity.InsuranceCompanyId = null;
            entity.InsuranceStatus = nameof(ServiceOrderDetailInsuranceStatusCode.NOT_APPLICABLE);
            entity.InsuranceRequestedAmount = 0m;
            entity.InsuranceApprovedAmount = 0m;
            entity.CustomerCoPayAmount = NormalizeNullableMoney(entity.CustomerCoPayAmount, entity.FinalAmount, nameof(entity.CustomerCoPayAmount));
            entity.GarageAbsorbedAmount = NormalizeNullableMoney(entity.GarageAbsorbedAmount, 0m, nameof(entity.GarageAbsorbedAmount));
        }

        private static void RecomputeFinancialAmounts(ServiceOrderDetail entity)
        {
            var quantity = decimal.Round(Math.Max(0m, entity.Quantity), 2, MidpointRounding.AwayFromZero);
            var unitPrice = decimal.Round(Math.Max(0m, entity.UnitPriceAtTime), 2, MidpointRounding.AwayFromZero);
            var discount = decimal.Round(Math.Max(0m, entity.DiscountAllocatedAmount ?? 0m), 2, MidpointRounding.AwayFromZero);
            var taxRate = decimal.Round(Math.Max(0m, entity.TaxRate), 2, MidpointRounding.AwayFromZero);

            var subTotal = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            var taxableBase = decimal.Round(Math.Max(0m, subTotal - discount), 2, MidpointRounding.AwayFromZero);
            var taxAmount = decimal.Round(taxableBase * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
            var finalAmount = decimal.Round(Math.Max(0m, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);

            entity.Quantity = quantity;
            entity.UnitPriceAtTime = unitPrice;
            entity.DiscountAllocatedAmount = discount;
            entity.TaxRate = taxRate;
            entity.SubTotal = NormalizeMoney(subTotal, "SubTotal");
            entity.TaxAmount = NormalizeMoney(taxAmount, "TaxAmount");
            entity.FinalAmount = NormalizeMoney(finalAmount, "FinalAmount");
        }

        private static decimal NormalizeMoney(decimal value, string fieldName)
        {
            if (value < 0m)
            {
                throw new InvalidOperationException($"{fieldName} không được âm.");
            }

            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal NormalizeNullableMoney(decimal? value, decimal fallback, string fieldName)
        {
            var resolved = value ?? fallback;
            if (resolved < 0m)
            {
                throw new InvalidOperationException($"{fieldName} không được âm.");
            }

            return decimal.Round(resolved, 2, MidpointRounding.AwayFromZero);
        }
    }
}
