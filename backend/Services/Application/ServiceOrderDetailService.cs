using System;
using System.Linq;
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
            await NormalizeAndValidateSupplementaryMetadataAsync(entity);
            await NormalizeAndValidateInsuranceAsync(entity);
            var created = await base.AddAsync(entity);
            await SyncServiceOrderWaitingPartsStatusAsync(created.ServiceOrderId, created.UpdatedBy ?? created.CreatedBy);
            return created;
        }

        public override async Task<ServiceOrderDetail?> UpdateAsync(object id, ServiceOrderDetail entity)
        {
            await NormalizeAndValidateSupplementaryMetadataAsync(entity);
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

        private async Task NormalizeAndValidateSupplementaryMetadataAsync(ServiceOrderDetail entity)
        {
            if (entity == null)
            {
                throw new InvalidOperationException("Dữ liệu chi tiết lệnh sửa chữa không hợp lệ.");
            }

            entity.ServiceSource = NormalizeServiceSource(entity.ServiceSource);
            entity.SupplementaryReasonCode = NormalizeSupplementaryReason(entity.SupplementaryReasonCode);

            if (!entity.IsSupplementary)
            {
                entity.ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
                entity.SupplementaryReasonCode = null;
                entity.SupplementaryRequestedByStaffId = null;
                entity.SupplementaryRequestedAt = null;
                entity.CustomerApprovedAt = null;
                await TryFillSourceQuotationIdAsync(entity);
                return;
            }

            if (!entity.QuotationDetailId.HasValue || entity.QuotationDetailId.Value <= 0)
            {
                throw new InvalidOperationException("Dịch vụ phát sinh bắt buộc phải đi qua báo giá (thiếu QuotationDetailId).");
            }

            if (!entity.SourceQuotationId.HasValue || entity.SourceQuotationId.Value <= 0)
            {
                throw new InvalidOperationException("Dịch vụ phát sinh bắt buộc phải gắn SourceQuotationId hợp lệ.");
            }

            await EnsureSupplementaryQuotationConsistencyAsync(entity);

            var normalizedSource = entity.ServiceSource;
            if (string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.INITIAL), StringComparison.OrdinalIgnoreCase))
            {
                normalizedSource = string.Equals(entity.SupplementaryReasonCode, nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING), StringComparison.OrdinalIgnoreCase)
                    ? nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN)
                    : nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER);
            }

            if (string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN), StringComparison.OrdinalIgnoreCase))
            {
                entity.ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN);
                entity.SupplementaryReasonCode = nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING);
            }
            else if (string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER), StringComparison.OrdinalIgnoreCase))
            {
                entity.ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER);
                entity.SupplementaryReasonCode = nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST);
            }
            else
            {
                throw new InvalidOperationException("ServiceSource không hợp lệ cho dịch vụ phát sinh.");
            }

            if (!entity.SupplementaryRequestedByStaffId.HasValue || entity.SupplementaryRequestedByStaffId.Value <= 0)
            {
                entity.SupplementaryRequestedByStaffId = entity.UpdatedBy ?? entity.CreatedBy;
            }

            entity.SupplementaryRequestedAt ??= VietnamTime.Now;
            entity.CustomerApprovedAt ??= VietnamTime.Now;
        }

        private async Task TryFillSourceQuotationIdAsync(ServiceOrderDetail entity)
        {
            if (entity.SourceQuotationId.HasValue && entity.SourceQuotationId.Value > 0)
            {
                return;
            }

            if (!entity.QuotationDetailId.HasValue || entity.QuotationDetailId.Value <= 0)
            {
                return;
            }

            entity.SourceQuotationId = await _context.QuotationDetails.AsNoTracking()
                .Where(x => x.Id == entity.QuotationDetailId.Value)
                .Select(x => x.QuotationId)
                .FirstOrDefaultAsync();
        }

        private async Task EnsureSupplementaryQuotationConsistencyAsync(ServiceOrderDetail entity)
        {
            var quotationMeta = await _context.QuotationDetails.AsNoTracking()
                .Where(x => x.Id == entity.QuotationDetailId!.Value)
                .Join(
                    _context.Quotations.AsNoTracking(),
                    detail => detail.QuotationId,
                    quotation => quotation.Id,
                    (detail, quotation) => new
                    {
                        QuotationId = quotation.Id,
                        quotation.ServiceOrderId,
                        quotation.VersionNo,
                        quotation.CreatedAt,
                        quotation.CreatedBy
                    })
                .FirstOrDefaultAsync();

            if (quotationMeta == null)
            {
                throw new InvalidOperationException("Không tìm thấy báo giá gốc của hạng mục phát sinh.");
            }

            if (quotationMeta.QuotationId != entity.SourceQuotationId!.Value)
            {
                throw new InvalidOperationException("SourceQuotationId không khớp với QuotationDetailId.");
            }

            if (quotationMeta.ServiceOrderId != entity.ServiceOrderId)
            {
                throw new InvalidOperationException("Báo giá phát sinh không thuộc ServiceOrder hiện tại.");
            }

            if (quotationMeta.VersionNo <= 1)
            {
                throw new InvalidOperationException("Dịch vụ phát sinh chỉ được phép từ báo giá bổ sung (Quotation #2 trở lên).");
            }

            entity.SupplementaryRequestedAt ??= quotationMeta.CreatedAt ?? VietnamTime.Now;
            entity.SupplementaryRequestedByStaffId ??= quotationMeta.CreatedBy;
        }

        private static string NormalizeServiceSource(string? source)
        {
            var normalized = (source ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN),
                nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER),
                _ => nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
            };
        }

        private static string? NormalizeSupplementaryReason(string? reason)
        {
            var normalized = (reason ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING) => nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
                nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST) => nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST),
                _ => null
            };
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
