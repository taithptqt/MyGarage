using MyGarage.Application.Authorization;
using MyGarage.Application.Invoices.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Application.Invoices;

public sealed class InvoiceApplicationService : IInvoiceApplicationService
{
    private readonly IInvoiceApplicationStore _store;
    private readonly IInvoiceAuthorizationService _authService;

    public InvoiceApplicationService(
        IInvoiceApplicationStore store,
        IInvoiceAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
    }

    public async Task<InvoiceDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var canView = await _authService.CanViewInvoiceAsync(id, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập thông tin hóa đơn này.");
        }

        var invoice = await _store.GetInvoiceWithDetailsAsync(id, ct);
        return invoice == null ? null : MapToDto(invoice);
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(InvoiceCreateInput input, UserActorContext actor, CancellationToken ct = default)
    {
        long? branchId = null;
        if (input.ServiceOrderId.HasValue && input.ServiceOrderId.Value > 0)
        {
            branchId = await _store.GetBranchIdForServiceOrderAsync(input.ServiceOrderId.Value, ct);
        }
        else if (input.RetailOrderId.HasValue && input.RetailOrderId.Value > 0)
        {
            branchId = await _store.GetBranchIdForRetailOrderAsync(input.RetailOrderId.Value, ct);
        }

        var canCreate = await _authService.CanCreateInvoiceAsync(branchId, actor, ct);
        if (!canCreate)
        {
            throw new UnauthorizedAccessException("Không có quyền lập hóa đơn tại chi nhánh này.");
        }

        var now = VietnamTime.Now;
        var detailInputs = input.Details ?? new List<InvoiceDetailInput>();

        var lineCalculations = detailInputs.Select(d =>
            InvoiceLifecycleRules.CalculateLineAmounts(d.UnitPrice, d.Quantity, d.TaxRatePercent)
        ).ToList();

        var totals = InvoiceLifecycleRules.CalculateInvoiceTotals(lineCalculations, 0m);

        var invoice = new Invoice
        {
            ServiceOrderId = input.ServiceOrderId,
            RetailOrderId = input.RetailOrderId,
            VehicleId = input.VehicleId,
            Phone = input.Phone,
            Address = input.Address,
            CompanyName = input.CompanyName,
            TaxCode = input.TaxCode,
            SubTotalAmount = totals.SubTotal,
            TaxAmount = totals.TaxAmount,
            TotalAmount = totals.FinalAmount,
            PayerType = string.IsNullOrWhiteSpace(input.PayerType) ? "CUSTOMER" : input.PayerType.Trim(),
            PayerId = input.PayerId ?? actor.UserId,
            PaidAmount = 0m,
            PaymentStatus = InvoiceLifecycleRules.PaymentStatusUnpaid,
            IssuedAt = now,
            CreatedAt = now,
            CreatedBy = actor.UserId,
            UpdatedAt = now,
            UpdatedBy = actor.UserId
        };

        var details = detailInputs.Zip(lineCalculations, (d, c) => new InvoiceDetail
        {
            ProductId = d.ProductId,
            StandardServiceId = d.StandardServiceId,
            ItemType = string.IsNullOrWhiteSpace(d.ItemType) ? "PART" : d.ItemType.Trim(),
            ItemName = string.IsNullOrWhiteSpace(d.ItemName) ? "Mục hóa đơn" : d.ItemName.Trim(),
            Quantity = d.Quantity,
            UnitPriceAtTime = d.UnitPrice,
            Unit = "Cái",
            SubTotal = c.SubTotal,
            TaxRate = d.TaxRatePercent,
            TaxAmount = c.TaxAmount,
            FinalAmount = c.FinalAmount
        }).ToList();

        await _store.CreateInvoiceWithDetailsAsync(invoice, details, ct);

        invoice.InvoiceDetails = details;
        return MapToDto(invoice);
    }

    public async Task<InvoiceCalculationResultDto> CalculateTotalsAsync(long invoiceId, CancellationToken ct = default)
    {
        if (invoiceId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(invoiceId), "Mã hóa đơn không hợp lệ.");
        }

        var invoice = await _store.GetInvoiceWithDetailsAsync(invoiceId, ct);
        if (invoice == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy hóa đơn #{invoiceId}.");
        }

        var lines = invoice.InvoiceDetails.Select(d =>
            new InvoiceLifecycleRules.LineAmounts(
                d.SubTotal,
                d.TaxAmount ?? 0m,
                d.FinalAmount
            )
        ).ToList();

        var totals = InvoiceLifecycleRules.CalculateInvoiceTotals(lines, 0m);
        var paidAmount = invoice.PaidAmount ?? 0m;
        var remainingDue = InvoiceLifecycleRules.CalculateRemainingDue(totals.FinalAmount, paidAmount);
        var paymentStatus = InvoiceLifecycleRules.ResolvePaymentStatus(paidAmount, totals.FinalAmount);

        return new InvoiceCalculationResultDto(
            invoice.Id,
            totals.SubTotal,
            totals.TaxAmount,
            totals.DiscountAmount,
            totals.FinalAmount,
            paidAmount,
            remainingDue,
            paymentStatus
        );
    }

    public Task<long?> GetServiceOrderIdByInvoiceIdAsync(long invoiceId, CancellationToken ct = default)
    {
        if (invoiceId <= 0)
        {
            return Task.FromResult<long?>(null);
        }

        return _store.GetServiceOrderIdByInvoiceIdAsync(invoiceId, ct);
    }

    private static InvoiceDto MapToDto(Invoice i) => new(
        i.Id,
        i.ServiceOrderId,
        i.RetailOrderId,
        i.VehicleId,
        i.Phone,
        i.Address,
        i.CompanyName,
        i.TaxCode,
        i.SubTotalAmount,
        i.TaxAmount,
        i.TotalAmount,
        i.PayerType,
        i.PayerId,
        i.PaidAmount,
        i.PaymentStatus,
        i.IssuedAt,
        i.CreatedAt,
        i.InvoiceDetails.Select(d => new InvoiceDetailDto(
            d.Id,
            d.InvoiceId,
            d.ProductId,
            d.StandardServiceId,
            d.ItemType,
            d.ItemName,
            d.Quantity,
            d.UnitPriceAtTime,
            d.SubTotal,
            d.TaxRate,
            d.TaxAmount,
            d.FinalAmount
        )).ToList()
    );
}
