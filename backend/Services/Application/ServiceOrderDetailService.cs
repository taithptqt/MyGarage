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
            var created = await base.AddAsync(entity);
            await SyncServiceOrderWaitingPartsStatusAsync(created.ServiceOrderId, created.UpdatedBy ?? created.CreatedBy);
            return created;
        }

        public override async Task<ServiceOrderDetail?> UpdateAsync(object id, ServiceOrderDetail entity)
        {
            await NormalizeAndValidateInsuranceAsync(entity);
            var updated = await base.UpdateAsync(id, entity);
            if (updated != null)
            {
                await SyncServiceOrderWaitingPartsStatusAsync(updated.ServiceOrderId, updated.UpdatedBy ?? updated.CreatedBy);
            }

            return updated;
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            var serviceOrderId = await ResolveServiceOrderIdAsync(id);
            var deleted = await base.DeleteAsync(id);
            if (deleted && serviceOrderId > 0)
            {
                await SyncServiceOrderWaitingPartsStatusAsync(serviceOrderId);
            }

            return deleted;
        }

        public async Task SyncServiceOrderWaitingPartsStatusAsync(long serviceOrderId, long? actorStaffId = null)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (serviceOrder == null)
            {
                return;
            }

            var now = VietnamTime.Now;
            var hasWaitingParts = await _context.ServiceOrderDetails
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.Status != null &&
                    x.Status.ToUpper() == nameof(ServiceOrderDetailStatusCode.WAITING_PARTS));

            var currentStatus = (serviceOrder.Status ?? string.Empty).Trim().ToUpperInvariant();
            var nextStatus = currentStatus;

            if (hasWaitingParts)
            {
                if (!IsTerminalServiceOrderStatus(currentStatus))
                {
                    nextStatus = nameof(ServiceOrderLifecycleStatus.WAITING_PARTS);
                }
            }
            else if (string.Equals(currentStatus, nameof(ServiceOrderLifecycleStatus.WAITING_PARTS), StringComparison.OrdinalIgnoreCase))
            {
                nextStatus = nameof(ServiceOrderLifecycleStatus.IN_PROGRESS);
            }

            if (string.Equals(nextStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            serviceOrder.Status = nextStatus;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = actorStaffId ?? serviceOrder.UpdatedBy;
            await _context.SaveChangesAsync();
        }

        private async Task<long> ResolveServiceOrderIdAsync(object id)
        {
            if (id == null)
            {
                return 0;
            }

            if (!long.TryParse(id.ToString(), out var parsedId) || parsedId <= 0)
            {
                return 0;
            }

            return await _context.ServiceOrderDetails
                .AsNoTracking()
                .Where(x => x.Id == parsedId)
                .Select(x => x.ServiceOrderId)
                .FirstOrDefaultAsync();
        }

        private static bool IsTerminalServiceOrderStatus(string normalizedStatus)
        {
            return normalizedStatus is
                nameof(ServiceOrderLifecycleStatus.CANCELED) or
                nameof(ServiceOrderLifecycleStatus.CLOSED) or
                nameof(ServiceOrderLifecycleStatus.READY) or
                nameof(ServiceOrderLifecycleStatus.WAITING_PAYMENT);
        }

        private async Task NormalizeAndValidateInsuranceAsync(ServiceOrderDetail entity)
        {
            if (entity == null)
            {
                throw new InvalidOperationException("Dữ liệu chi tiết lệnh sửa chữa không hợp lệ.");
            }

            entity.PayerType = InsuranceBillingRules.NormalizePayerType(entity.PayerType);

            entity.SubTotal = NormalizeMoney(entity.SubTotal, "SubTotal");
            entity.TaxAmount = NormalizeMoney(entity.TaxAmount, "TaxAmount");
            entity.FinalAmount = NormalizeMoney(entity.FinalAmount, "FinalAmount");

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
