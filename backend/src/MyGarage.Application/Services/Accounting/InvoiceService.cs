using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Authorization;
using MyGarage.Application.Invoices;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class InvoiceService : GenericService<Invoice>, IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IInvoiceBackfillStore _backfillStore;
        private readonly IInvoiceOrderContextStore _orderContextStore;
        private readonly IInvoicePersistenceStore _persistenceStore;
        private readonly IInvoiceBranchLookupStore _branchLookup;
        private readonly IInvoiceAuthorizationService _authorization;
        private readonly IServiceOrderEditCapabilityService _editCapabilityService;
        private readonly IMapper _mapper;

        public InvoiceService(
            IInvoiceRepository repository,
            IInvoiceBackfillStore backfillStore,
            IInvoiceOrderContextStore orderContextStore,
            IInvoicePersistenceStore persistenceStore,
            IInvoiceBranchLookupStore branchLookup,
            IInvoiceAuthorizationService authorization,
            IServiceOrderEditCapabilityService editCapabilityService,
            IMapper mapper) : base(repository)
        {
            _invoiceRepository = repository;
            _backfillStore = backfillStore;
            _orderContextStore = orderContextStore;
            _persistenceStore = persistenceStore;
            _branchLookup = branchLookup;
            _authorization = authorization;
            _editCapabilityService = editCapabilityService;
            _mapper = mapper;
        }

        public List<InvoiceResponseDto> MapToDtos(IEnumerable<Invoice> items)
            => _mapper.Map<List<InvoiceResponseDto>>(items);

        public InvoiceResponseDto MapToDto(Invoice item)
            => _mapper.Map<InvoiceResponseDto>(item);

        public async Task<InvoiceResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<InvoiceResponseDto>(item);
        }

        public async Task<(IReadOnlyList<InvoiceResponseDto> Items, int Total)> GetScopedPageDtosAsync(
            long? branchId,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var (items, total) = await _invoiceRepository.GetScopedPageAsync(branchId, page, pageSize, ct);
            return (MapToDtos(items), total);
        }

        public async Task<InvoiceResponseDto?> GetScopedDtoByIdAsync(
            long id,
            long? branchId,
            CancellationToken ct = default)
        {
            var item = await _invoiceRepository.GetScopedAsync(id, branchId, ct);
            return item == null ? null : MapToDto(item);
        }

        public async Task<InvoiceMutationResult> CreateOrUpdateForContextAsync(
            InvoiceCreateDto? itemDto,
            long actorUserId,
            long? scopedBranchId,
            UserActorContext actor,
            CancellationToken ct = default)
        {
            if ((itemDto?.ServiceOrderId ?? 0) <= 0 && (itemDto?.RetailOrderId ?? 0) <= 0)
            {
                return new InvoiceMutationResult(
                    InvoiceMutationStatus.BadRequest,
                    ErrorMessage: "Invoice must be created from a service order or retail order.");
            }

            if ((itemDto?.ServiceOrderId ?? 0) > 0 && (itemDto?.RetailOrderId ?? 0) > 0)
            {
                return new InvoiceMutationResult(
                    InvoiceMutationStatus.BadRequest,
                    ErrorMessage: "Invoice must have exactly one source order.");
            }

            var sourceBranch = (itemDto?.ServiceOrderId ?? 0) > 0
                ? await _branchLookup.GetBranchIdForServiceOrderAsync(itemDto!.ServiceOrderId!.Value, ct)
                : await _branchLookup.GetBranchIdForRetailOrderAsync(itemDto!.RetailOrderId!.Value, ct);
            if (!sourceBranch.HasValue)
            {
                return new InvoiceMutationResult(InvoiceMutationStatus.NotFound);
            }

            if (scopedBranchId.HasValue && sourceBranch.Value != scopedBranchId.Value)
            {
                return new InvoiceMutationResult(InvoiceMutationStatus.Forbidden);
            }

            if (!await _authorization.CanCreateInvoiceAsync(sourceBranch, actor, ct))
            {
                return new InvoiceMutationResult(InvoiceMutationStatus.Forbidden);
            }

            var lockError = await ValidateInvoiceMutationAsync(itemDto?.ServiceOrderId);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return new InvoiceMutationResult(InvoiceMutationStatus.BadRequest, ErrorMessage: lockError);
            }

            try
            {
                var outcome = await EnsureForPaymentContextWithOutcomeAsync(
                    itemDto?.ServiceOrderId,
                    itemDto?.RetailOrderId,
                    itemDto?.PayerType,
                    itemDto?.PayerId);
                var createdItem = outcome.Invoice;
                if (createdItem == null)
                {
                    return new InvoiceMutationResult(
                        InvoiceMutationStatus.BadRequest,
                        ErrorMessage: "Không thể tạo hóa đơn từ nguồn dữ liệu hiện tại.");
                }

                createdItem.Phone = itemDto?.Phone ?? createdItem.Phone;
                createdItem.Address = itemDto?.Address ?? createdItem.Address;
                createdItem.InvoiceUrl = itemDto?.InvoiceUrl ?? createdItem.InvoiceUrl;
                createdItem.CompanyName = itemDto?.CompanyName ?? createdItem.CompanyName;
                createdItem.CompanyAddress = itemDto?.CompanyAddress ?? createdItem.CompanyAddress;
                createdItem.ReceiveEmail = itemDto?.ReceiveEmail ?? createdItem.ReceiveEmail;
                createdItem.TaxCode = itemDto?.TaxCode ?? createdItem.TaxCode;
                createdItem.CreditNote = itemDto?.CreditNote ?? createdItem.CreditNote;
                createdItem.IssuedAt ??= itemDto?.IssuedAt;
                createdItem.DueDate ??= itemDto?.DueDate?.Date;
                createdItem.CreatedBy ??= actorUserId;
                createdItem.UpdatedBy = actorUserId;
                createdItem.UpdatedAt = VietnamTime.Now;

                createdItem = await UpdateAsync(createdItem.Id, createdItem) ?? createdItem;
                var responseDto = MapToDto(createdItem);
                return new InvoiceMutationResult(
                    outcome.Created ? InvoiceMutationStatus.Created : InvoiceMutationStatus.Ok,
                    Data: responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return new InvoiceMutationResult(InvoiceMutationStatus.BadRequest, ErrorMessage: ex.Message);
            }
        }

        private async Task<string?> ValidateInvoiceMutationAsync(long? serviceOrderId)
        {
            if (!serviceOrderId.HasValue || serviceOrderId.Value <= 0)
            {
                return null;
            }

            var capability = await _editCapabilityService.GetEditCapabilityAsync(serviceOrderId.Value);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý hóa đơn.";
            }

            if (capability.IsClosedLocked)
            {
                return "Không thể chỉnh sửa hóa đơn vì service order đã đóng.";
            }

            if (capability.IsBillingLocked)
            {
                return "Không thể chỉnh sửa hóa đơn vì lệnh đã chốt tài chính.";
            }

            return null;
        }

        public async Task<Invoice?> EnsureForPaymentContextAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId)
            => (await EnsureForPaymentContextWithOutcomeAsync(serviceOrderId, retailOrderId, payerType, payerId)).Invoice;

        public async Task<(Invoice? Invoice, bool Created)> EnsureForPaymentContextWithOutcomeAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId)
        {
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                return await EnsureServiceOrderInvoiceAsync(serviceOrderId.Value, payerType, payerId);
            }

            if (retailOrderId.HasValue && retailOrderId.Value > 0)
            {
                return await EnsureRetailOrderInvoiceAsync(retailOrderId.Value, payerType, payerId);
            }

            return (null, false);
        }

        public async Task<InvoiceBackfillResponseDto> BackfillCompletedPaymentsAsync()
        {
            var result = new InvoiceBackfillResponseDto();

            var completedPayments = await _backfillStore.GetCompletedPaymentsForBackfillAsync();
            result.CompletedPaymentsScanned = completedPayments.Count;

            foreach (var payment in completedPayments)
            {
                var hadInvoiceId = payment.InvoiceId.HasValue && payment.InvoiceId.Value > 0;
                // Backfill only missing links; existing links require separate reconciliation.
                if (hadInvoiceId)
                {
                    continue;
                }

                var beforeInvoiceIds = await _backfillStore.GetAllInvoiceIdsAsync();
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

                var afterInvoiceIds = await _backfillStore.GetAllInvoiceIdsAsync();
                foreach (var invoiceId in afterInvoiceIds.Where(id => !beforeInvoiceSet.Contains(id)))
                {
                    result.CreatedInvoices++;
                    result.InvoiceIdsCreated.Add(invoiceId);
                }
            }

            await _persistenceStore.SaveChangesInTransactionAsync();
            return result;
        }

        private async Task<(Invoice? Invoice, bool Created)> EnsureServiceOrderInvoiceAsync(long serviceOrderId, string? payerType, long? payerId)
        {
            var serviceOrder = await _orderContextStore.GetServiceOrderWithDetailsForInvoiceAsync(serviceOrderId);
            if (serviceOrder == null) return (null, false);

            var normalizedPayer = RequirePayerType(payerType);
            var resolvedPayerId = ResolvePayerId(normalizedPayer, payerId, serviceOrder.CustomerId);
            if (resolvedPayerId is not > 0 || (normalizedPayer == "CUSTOMER" && resolvedPayerId != serviceOrder.CustomerId))
                throw new InvalidOperationException("Không xác định được bên thanh toán phù hợp với lệnh sửa chữa.");

            var invoices = await _orderContextStore.GetInvoicesByServiceOrderIdAsync(serviceOrderId);
            var invoice = ResolveExistingInvoice(invoices, normalizedPayer, resolvedPayerId.Value, serviceOrderId, null);
            if (invoice != null) return (invoice, false);
            if (invoices.Count != 0)
                throw new InvalidOperationException("Không thể tự tạo hóa đơn khác khi lệnh đã có hóa đơn; vui lòng chọn hóa đơn phù hợp.");

            var activeDetails = serviceOrder.ServiceOrderDetails
                .Where(d => !string.Equals(d.Status, nameof(ServiceOrderDetailStatusCode.CANCELED), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (normalizedPayer != "CUSTOMER" || activeDetails.Any(d =>
                    !string.Equals(d.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) ||
                    d.InsuranceCompanyId is > 0 || d.InsuranceRequestedAmount.GetValueOrDefault() > 0m ||
                    d.InsuranceApprovedAmount.GetValueOrDefault() > 0m || d.GarageAbsorbedAmount.GetValueOrDefault() > 0m) ||
                !serviceOrder.FinalAmount.HasValue || activeDetails.Sum(d => d.FinalAmount) != serviceOrder.FinalAmount.Value)
                throw new InvalidOperationException("Không thể tự tạo hóa đơn khi chưa xác định an toàn phần thanh toán từ báo giá đã duyệt.");

            var now = VietnamTime.Now;
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

            await _persistenceStore.AddInvoiceAndSaveAsync(invoice);

            await PopulateServiceOrderInvoiceDetailsAsync(invoice, activeDetails);
            await _persistenceStore.SaveChangesInTransactionAsync();
            return (invoice, true);
        }

        private async Task<(Invoice? Invoice, bool Created)> EnsureRetailOrderInvoiceAsync(long retailOrderId, string? payerType, long? payerId)
        {
            var retailOrder = await _orderContextStore.GetRetailOrderWithItemsForInvoiceAsync(retailOrderId);
            if (retailOrder == null) return (null, false);

            var normalizedPayer = RequirePayerType(payerType);
            var resolvedPayerId = ResolvePayerId(normalizedPayer, payerId, retailOrder.CustomerId);
            if (normalizedPayer != "CUSTOMER" || resolvedPayerId is not > 0 || resolvedPayerId != retailOrder.CustomerId)
                throw new InvalidOperationException("Không xác định được bên thanh toán phù hợp với đơn bán lẻ.");

            var invoices = await _orderContextStore.GetInvoicesByRetailOrderIdAsync(retailOrderId);
            var invoice = ResolveExistingInvoice(invoices, normalizedPayer, resolvedPayerId.Value, null, retailOrderId);
            if (invoice != null) return (invoice, false);
            if (invoices.Count != 0)
                throw new InvalidOperationException("Không thể tự tạo hóa đơn khác khi đơn đã có hóa đơn; vui lòng chọn hóa đơn phù hợp.");

            var now = VietnamTime.Now;
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

            await _persistenceStore.AddInvoiceAndSaveAsync(invoice);

            await PopulateRetailOrderInvoiceDetailsAsync(invoice, retailOrder.RetailOrderItems.ToList());
            await _persistenceStore.SaveChangesInTransactionAsync();
            return (invoice, true);
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
                await _persistenceStore.AddInvoiceDetailsAndSaveAsync(missingDetails);
            }
        }

        private async Task PopulateRetailOrderInvoiceDetailsAsync(Invoice invoice, List<RetailOrderItem> items)
        {
            if (invoice.InvoiceDetails.Count > 0)
            {
                return;
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
                await _persistenceStore.AddInvoiceDetailsAndSaveAsync(invoiceDetails);
            }
        }

        private static string RequirePayerType(string? payerType)
        {
            var normalized = payerType?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(normalized)) return "CUSTOMER";
            if (normalized is "CUSTOMER" or "INSURANCE") return normalized;
            throw new InvalidOperationException("Loại bên thanh toán không hợp lệ.");
        }

        private static Invoice? ResolveExistingInvoice(
            IReadOnlyList<Invoice> invoices, string payerType, long payerId, long? serviceOrderId, long? retailOrderId)
        {
            var matches = invoices.Where(invoice =>
                invoice.ServiceOrderId == serviceOrderId && invoice.RetailOrderId == retailOrderId &&
                invoice.PayerId == payerId && string.Equals(invoice.PayerType?.Trim(), payerType, StringComparison.OrdinalIgnoreCase))
                .Take(2).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException("Có nhiều hóa đơn phù hợp; vui lòng chọn hóa đơn cụ thể.");
            return matches.SingleOrDefault();
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
