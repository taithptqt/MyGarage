using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Invoices;
using MyGarage.Application.Invoices.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Services;

public sealed class InvoiceApplicationService : IInvoiceApplicationService
{
    private readonly IGarageContext _context;
    private readonly IInvoiceAuthorizationService _authService;

    public InvoiceApplicationService(
        IGarageContext context,
        IInvoiceAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    public async Task<InvoiceDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var canView = await _authService.CanViewInvoiceAsync(id, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập thông tin hóa đơn này.");
        }

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceDetails)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return invoice == null ? null : MapToDto(invoice);
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(InvoiceCreateInput input, UserActorContext actor, CancellationToken ct = default)
    {
        long? branchId = null;
        if (input.ServiceOrderId.HasValue && input.ServiceOrderId.Value > 0)
        {
            branchId = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => so.Id == input.ServiceOrderId.Value)
                .Select(so => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(ct);
        }
        else if (input.RetailOrderId.HasValue && input.RetailOrderId.Value > 0)
        {
            branchId = await _context.RetailOrders
                .AsNoTracking()
                .Where(ro => ro.Id == input.RetailOrderId.Value)
                .Select(ro => (long?)ro.BranchId)
                .FirstOrDefaultAsync(ct);
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync(ct);

                foreach (var detail in details)
                {
                    detail.InvoiceId = invoice.Id;
                }
                _context.InvoiceDetails.AddRange(details);
                await _context.SaveChangesAsync(ct);

                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
        });

        invoice.InvoiceDetails = details;
        return MapToDto(invoice);
    }

    public async Task<InvoiceCalculationResultDto> CalculateTotalsAsync(long invoiceId, CancellationToken ct = default)
    {
        if (invoiceId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(invoiceId), "Mã hóa đơn không hợp lệ.");
        }

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceDetails)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

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
