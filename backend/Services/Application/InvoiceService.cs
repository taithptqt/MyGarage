using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InvoiceService : GenericService<Invoice>, IInvoiceService
    {
        private readonly IGarageContext _context;

        public InvoiceService(IInvoiceRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public async Task<Invoice?> EnsureForPaymentContextAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId)
        {
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                return await EnsureServiceOrderInvoiceAsync(serviceOrderId.Value, payerType, payerId);
            }

            if (retailOrderId.HasValue && retailOrderId.Value > 0)
            {
                return await EnsureRetailOrderInvoiceAsync(retailOrderId.Value, payerType, payerId);
            }

            return null;
        }

        public async Task<InvoiceBackfillResponseDto> BackfillCompletedPaymentsAsync()
        {
            var result = new InvoiceBackfillResponseDto();

            var completedPayments = await _context.Payments
                .Where(payment =>
                    payment.Status != null &&
                    payment.Status.ToUpper() == nameof(PaymentStatusCode.COMPLETED) &&
                    ((payment.ServiceOrderId.HasValue && payment.ServiceOrderId.Value > 0) ||
                     (payment.RetailOrderId.HasValue && payment.RetailOrderId.Value > 0)))
                .OrderBy(payment => payment.Id)
                .ToListAsync();

            result.CompletedPaymentsScanned = completedPayments.Count;

            foreach (var payment in completedPayments)
            {
                var hadInvoiceId = payment.InvoiceId.HasValue && payment.InvoiceId.Value > 0;

                var beforeInvoiceIds = await _context.Invoices
                    .AsNoTracking()
                    .Select(invoice => invoice.Id)
                    .ToListAsync();
                var beforeInvoiceSet = beforeInvoiceIds.ToHashSet();

                var invoice = await EnsureForPaymentContextAsync(
                    payment.ServiceOrderId,
                    payment.RetailOrderId,
                    payment.PayerType,
                    payment.PayerId);

                if (invoice == null)
                {
                    continue;
                }

                if (!hadInvoiceId || payment.InvoiceId != invoice.Id)
                {
                    payment.InvoiceId = invoice.Id;
                    payment.Direction = "INFLOW";
                    payment.UpdatedAt = VietnamTime.Now;
                    result.LinkedPayments++;
                    result.PaymentIdsUpdated.Add(payment.Id);
                }

                var afterInvoiceIds = await _context.Invoices
                    .AsNoTracking()
                    .Select(created => created.Id)
                    .ToListAsync();
                foreach (var invoiceId in afterInvoiceIds.Where(id => !beforeInvoiceSet.Contains(id)))
                {
                    result.CreatedInvoices++;
                    result.InvoiceIdsCreated.Add(invoiceId);
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<Invoice?> EnsureServiceOrderInvoiceAsync(long serviceOrderId, string? payerType, long? payerId)
        {
            var invoice = await _context.Invoices
                .Include(x => x.InvoiceDetails)
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            var serviceOrder = await _context.ServiceOrders
                .Include(x => x.Customer)
                .Include(x => x.Vehicle)
                .Include(x => x.ServiceOrderDetails)
                .FirstOrDefaultAsync(x => x.Id == serviceOrderId);

            if (serviceOrder == null)
            {
                return invoice;
            }

            var now = VietnamTime.Now;
            if (invoice == null)
            {
                invoice = new Invoice
                {
                    ServiceOrderId = serviceOrder.Id,
                    VehicleId = serviceOrder.VehicleId,
                    Phone = serviceOrder.Customer?.Phone,
                    Address = null,
                    ReceiveEmail = serviceOrder.Customer?.Email,
                    SubTotalAmount = serviceOrder.SubTotalAmount ?? 0m,
                    TaxAmount = serviceOrder.TaxAmount ?? 0m,
                    TotalAmount = serviceOrder.FinalAmount ?? 0m,
                    PayerType = NormalizePayerType(payerType),
                    PayerId = ResolvePayerId(payerType, payerId, serviceOrder.CustomerId),
                    PaidAmount = 0m,
                    PaymentStatus = nameof(InvoicePaymentStatusCode.UNPAID),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
            }
            else
            {
                invoice.VehicleId = serviceOrder.VehicleId;
                invoice.Phone = serviceOrder.Customer?.Phone;
                invoice.ReceiveEmail = serviceOrder.Customer?.Email;
                invoice.SubTotalAmount = serviceOrder.SubTotalAmount ?? 0m;
                invoice.TaxAmount = serviceOrder.TaxAmount ?? 0m;
                invoice.TotalAmount = serviceOrder.FinalAmount ?? 0m;
                invoice.PayerType = NormalizePayerType(payerType);
                invoice.PayerId = ResolvePayerId(payerType, payerId, serviceOrder.CustomerId);
                invoice.UpdatedAt = now;
            }

            await PopulateServiceOrderInvoiceDetailsAsync(invoice, serviceOrder.ServiceOrderDetails.ToList());
            await RecomputeInvoicePaymentStatusAsync(invoice.Id);
            await _context.SaveChangesAsync();
            return invoice;
        }

        private async Task<Invoice?> EnsureRetailOrderInvoiceAsync(long retailOrderId, string? payerType, long? payerId)
        {
            var invoice = await _context.Invoices
                .Include(x => x.InvoiceDetails)
                .Where(x => x.RetailOrderId == retailOrderId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            var retailOrder = await _context.RetailOrders
                .Include(x => x.Customer)
                .Include(x => x.RetailOrderItems)
                    .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == retailOrderId);

            if (retailOrder == null)
            {
                return invoice;
            }

            var now = VietnamTime.Now;
            if (invoice == null)
            {
                invoice = new Invoice
                {
                    RetailOrderId = retailOrder.Id,
                    Phone = retailOrder.Customer?.Phone,
                    Address = null,
                    ReceiveEmail = retailOrder.Customer?.Email,
                    SubTotalAmount = retailOrder.SubTotal,
                    TaxAmount = retailOrder.TaxAmount ?? 0m,
                    TotalAmount = retailOrder.FinalAmount,
                    PayerType = NormalizePayerType(payerType),
                    PayerId = ResolvePayerId(payerType, payerId, retailOrder.CustomerId),
                    PaidAmount = 0m,
                    PaymentStatus = nameof(InvoicePaymentStatusCode.UNPAID),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
            }
            else
            {
                invoice.Phone = retailOrder.Customer?.Phone;
                invoice.ReceiveEmail = retailOrder.Customer?.Email;
                invoice.SubTotalAmount = retailOrder.SubTotal;
                invoice.TaxAmount = retailOrder.TaxAmount ?? 0m;
                invoice.TotalAmount = retailOrder.FinalAmount;
                invoice.PayerType = NormalizePayerType(payerType);
                invoice.PayerId = ResolvePayerId(payerType, payerId, retailOrder.CustomerId);
                invoice.UpdatedAt = now;
            }

            await PopulateRetailOrderInvoiceDetailsAsync(invoice, retailOrder.RetailOrderItems.ToList());
            await RecomputeInvoicePaymentStatusAsync(invoice.Id);
            await _context.SaveChangesAsync();
            return invoice;
        }

        private async Task PopulateServiceOrderInvoiceDetailsAsync(Invoice invoice, List<ServiceOrderDetail> details)
        {
            var existingReferenceIds = invoice.InvoiceDetails
                .Where(x => x.ReferenceServiceOrderDetailId.HasValue)
                .Select(x => x.ReferenceServiceOrderDetailId!.Value)
                .ToHashSet();

            var missingDetails = details
                .Where(detail => detail.Quantity > 0 && detail.FinalAmount >= 0m)
                .Where(detail => !existingReferenceIds.Contains(detail.Id))
                .Select(detail => new InvoiceDetail
                {
                    InvoiceId = invoice.Id,
                    ReferenceServiceOrderDetailId = detail.Id,
                    ItemType = NormalizeInvoiceDetailItemType(detail.ItemType, hasStandardService: detail.StandardServiceId.HasValue),
                    ProductId = ResolveProductId(detail),
                    StandardServiceId = detail.StandardServiceId,
                    ItemName = detail.ItemName,
                    Quantity = detail.Quantity,
                    CostPriceAtTime = detail.CostPriceAtTime,
                    UnitPriceAtTime = detail.UnitPriceAtTime,
                    Unit = string.IsNullOrWhiteSpace(detail.Unit) ? "lần" : detail.Unit,
                    SubTotal = detail.SubTotal,
                    DiscountAllocatedAmount = detail.DiscountAllocatedAmount,
                    TaxRate = detail.TaxRate,
                    TaxAmount = detail.TaxAmount,
                    FinalAmount = detail.FinalAmount,
                    Note = detail.Note
                })
                .ToList();

            if (missingDetails.Count > 0)
            {
                _context.InvoiceDetails.AddRange(missingDetails);
                await _context.SaveChangesAsync();
            }
        }

        private async Task PopulateRetailOrderInvoiceDetailsAsync(Invoice invoice, List<RetailOrderItem> items)
        {
            if (invoice.InvoiceDetails.Count > 0)
            {
                _context.InvoiceDetails.RemoveRange(invoice.InvoiceDetails);
                invoice.InvoiceDetails.Clear();
            }

            var invoiceDetails = items
                .Where(item => item.Quantity > 0)
                .Select(item => new InvoiceDetail
                {
                    InvoiceId = invoice.Id,
                    ReferenceServiceOrderDetailId = null,
                    ItemType = "PART",
                    ProductId = item.ProductId,
                    StandardServiceId = null,
                    ItemName = item.Product?.Name ?? $"Product #{item.ProductId}",
                    Quantity = item.Quantity,
                    CostPriceAtTime = item.CostPriceAtTime,
                    UnitPriceAtTime = item.UnitPriceAtTime,
                    Unit = string.IsNullOrWhiteSpace(item.Product?.Unit) ? "cái" : item.Product!.Unit,
                    SubTotal = item.SubTotal,
                    DiscountAllocatedAmount = 0m,
                    TaxRate = item.TaxRate,
                    TaxAmount = item.TaxAmount,
                    FinalAmount = item.FinalAmount,
                    Note = null
                })
                .ToList();

            if (invoiceDetails.Count > 0)
            {
                _context.InvoiceDetails.AddRange(invoiceDetails);
                await _context.SaveChangesAsync();
            }
        }

        private async Task RecomputeInvoicePaymentStatusAsync(long invoiceId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId);
            if (invoice == null)
            {
                return;
            }

            var paidAmount = await _context.Payments
                .Where(x =>
                    x.InvoiceId == invoiceId &&
                    x.Status != null &&
                    x.Status.ToUpper() == nameof(PaymentStatusCode.COMPLETED))
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            invoice.PaidAmount = paidAmount;
            var invoiceTotal = invoice.TotalAmount ?? 0m;
            invoice.PaymentStatus = paidAmount <= 0m
                ? nameof(InvoicePaymentStatusCode.UNPAID)
                : paidAmount < invoiceTotal
                    ? nameof(InvoicePaymentStatusCode.PARTIAL)
                    : nameof(InvoicePaymentStatusCode.PAID);
            invoice.UpdatedAt = VietnamTime.Now;
        }

        private static string NormalizePayerType(string? payerType)
        {
            var normalized = payerType?.Trim().ToUpperInvariant();
            return normalized is "INSURANCE" or "CUSTOMER" ? normalized : "CUSTOMER";
        }

        private static long? ResolvePayerId(string? payerType, long? payerId, long? fallbackCustomerId)
        {
            if (NormalizePayerType(payerType) == "CUSTOMER")
            {
                return payerId ?? fallbackCustomerId;
            }

            return payerId;
        }

        private static long? ResolveProductId(ServiceOrderDetail detail)
        {
            var itemType = (detail.ItemType ?? string.Empty).Trim().ToUpperInvariant();
            if (itemType is "PART" or "PARTS" or "PRODUCT")
            {
                return detail.ItemId;
            }

            if (!detail.StandardServiceId.HasValue && detail.ItemId.HasValue)
            {
                return detail.ItemId.Value;
            }

            return null;
        }

        private static string NormalizeInvoiceDetailItemType(string? itemType, bool hasStandardService)
        {
            var normalized = (itemType ?? string.Empty).Trim().ToUpperInvariant();

            return normalized switch
            {
                "PART" or "PARTS" or "PRODUCT" => "PART",
                "PACKAGE" => "PACKAGE",
                "SUBCONTRACT" => "SUBCONTRACT",
                "OTHER" => "OTHER",
                "LABOR" or "SERVICE" => "LABOR",
                _ => hasStandardService ? "LABOR" : "PART"
            };
        }
    }
}
