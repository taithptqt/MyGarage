using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Accounting;
using MyGarage.Application.Accounting.DTOs;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common;
using MyGarage.Application.Invoices;
using MyGarage.Application.Invoices.DTOs;
using MyGarage.Application.Payments;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Infrastructure.Invoices;
using MyGarage.Infrastructure.Payments;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class PaymentAndAccountingModuleTests
{
    // =========================================================================
    // SECTION 1: Payment Pure Domain Rules (PaymentLifecycleRules)
    // =========================================================================

    [Fact]
    public void PaymentRules_NormalizeStatus_HandlesAliasesAndDefaults()
    {
        Assert.Equal("COMPLETED", PaymentLifecycleRules.NormalizeStatus("PAID"));
        Assert.Equal("COMPLETED", PaymentLifecycleRules.NormalizeStatus("SUCCESS"));
        Assert.Equal("COMPLETED", PaymentLifecycleRules.NormalizeStatus("completed"));
        Assert.Equal("CANCELED", PaymentLifecycleRules.NormalizeStatus("CANCELLED"));
        Assert.Equal("CANCELED", PaymentLifecycleRules.NormalizeStatus("canceled"));
        Assert.Equal("FAILED", PaymentLifecycleRules.NormalizeStatus("ERROR"));
        Assert.Equal("FAILED", PaymentLifecycleRules.NormalizeStatus("failed"));
        Assert.Equal("PROCESSING", PaymentLifecycleRules.NormalizeStatus("processing"));
        Assert.Equal("PENDING", PaymentLifecycleRules.NormalizeStatus("pending"));
        Assert.Equal("PENDING", PaymentLifecycleRules.NormalizeStatus(null));
        Assert.Equal("PENDING", PaymentLifecycleRules.NormalizeStatus("unknown_status"));
    }

    [Fact]
    public void PaymentRules_CanTransition_EnforcesStrictFsm()
    {
        // Valid forward transitions
        Assert.True(PaymentLifecycleRules.CanTransition("PENDING", "PROCESSING"));
        Assert.True(PaymentLifecycleRules.CanTransition("PENDING", "COMPLETED"));
        Assert.True(PaymentLifecycleRules.CanTransition("PENDING", "FAILED"));
        Assert.True(PaymentLifecycleRules.CanTransition("PENDING", "CANCELED"));
        Assert.True(PaymentLifecycleRules.CanTransition("PROCESSING", "COMPLETED"));
        Assert.True(PaymentLifecycleRules.CanTransition("PROCESSING", "FAILED"));
        Assert.True(PaymentLifecycleRules.CanTransition("PROCESSING", "CANCELED"));

        // Self-transitions
        Assert.True(PaymentLifecycleRules.CanTransition("PENDING", "PENDING"));
        Assert.True(PaymentLifecycleRules.CanTransition("COMPLETED", "COMPLETED"));
        Assert.True(PaymentLifecycleRules.CanTransition("FAILED", "FAILED"));
        Assert.True(PaymentLifecycleRules.CanTransition("CANCELED", "CANCELED"));

        // Terminal statuses cannot transition to any other status
        Assert.False(PaymentLifecycleRules.CanTransition("COMPLETED", "PENDING"));
        Assert.False(PaymentLifecycleRules.CanTransition("COMPLETED", "FAILED"));
        Assert.False(PaymentLifecycleRules.CanTransition("COMPLETED", "CANCELED"));
        Assert.False(PaymentLifecycleRules.CanTransition("FAILED", "COMPLETED"));
        Assert.False(PaymentLifecycleRules.CanTransition("CANCELED", "COMPLETED"));

        // Invalid backward transitions
        Assert.False(PaymentLifecycleRules.CanTransition("PROCESSING", "PENDING"));
    }

    [Fact]
    public void PaymentRules_ValidateTransition_ReturnsDescriptiveErrors()
    {
        var (valid1, err1) = PaymentLifecycleRules.ValidateTransition("PENDING", "COMPLETED");
        Assert.True(valid1);
        Assert.Null(err1);

        var (valid2, err2) = PaymentLifecycleRules.ValidateTransition("COMPLETED", "PENDING");
        Assert.False(valid2);
        Assert.Contains("immutable terminal status", err2);

        var (valid3, err3) = PaymentLifecycleRules.ValidateTransition("PROCESSING", "PENDING");
        Assert.False(valid3);
        Assert.Contains("Invalid payment status transition", err3);

        var (valid4, err4) = PaymentLifecycleRules.ValidateTransition("PENDING", "");
        Assert.False(valid4);
        Assert.Contains("cannot be empty", err4);
    }

    [Fact]
    public void PaymentRules_NormalizeDirection_InflowRequiresInvoiceInvariant()
    {
        // When InvoiceId > 0, direction is ALWAYS INFLOW
        Assert.Equal("INFLOW", PaymentLifecycleRules.NormalizeDirection(null, 100));
        Assert.Equal("INFLOW", PaymentLifecycleRules.NormalizeDirection("OUTFLOW", 100));
        Assert.Equal("INFLOW", PaymentLifecycleRules.NormalizeDirection("INFLOW", 100));

        // When InvoiceId is missing or null, direction defaults to OUTFLOW if specified, else INFLOW
        Assert.Equal("OUTFLOW", PaymentLifecycleRules.NormalizeDirection("OUTFLOW", null));
        Assert.Equal("OUTFLOW", PaymentLifecycleRules.NormalizeDirection("outflow", 0));
        Assert.Equal("INFLOW", PaymentLifecycleRules.NormalizeDirection("INFLOW", null));
        Assert.Equal("INFLOW", PaymentLifecycleRules.NormalizeDirection(null, null));
    }

    [Fact]
    public void PaymentRules_NormalizeWebhookOutcome_NormalizesCorrectly()
    {
        Assert.Equal("COMPLETED", PaymentLifecycleRules.NormalizeWebhookOutcome("PAID", 100_000m, 100_000m));
        Assert.Equal("COMPLETED", PaymentLifecycleRules.NormalizeWebhookOutcome("SUCCESS", 0m, 100_000m));
        Assert.Equal("COMPLETED", PaymentLifecycleRules.NormalizeWebhookOutcome(null, 150_000m, 100_000m));

        Assert.Equal("CANCELED", PaymentLifecycleRules.NormalizeWebhookOutcome("CANCELED", 0m, 100_000m));
        Assert.Equal("CANCELED", PaymentLifecycleRules.NormalizeWebhookOutcome("CANCELLED", 0m, 100_000m));

        Assert.Equal("FAILED", PaymentLifecycleRules.NormalizeWebhookOutcome("FAILED", 0m, 100_000m));
        Assert.Equal("FAILED", PaymentLifecycleRules.NormalizeWebhookOutcome("ERROR", 0m, 100_000m));

        Assert.Equal("PROCESSING", PaymentLifecycleRules.NormalizeWebhookOutcome(null, 50_000m, 100_000m));
        Assert.Equal("PENDING", PaymentLifecycleRules.NormalizeWebhookOutcome(null, 0m, 100_000m));
    }

    [Fact]
    public void PaymentRules_IsDuplicateOrIdempotentWebhook_PreventsDoubleProcessing()
    {
        // Completed payment receiving another completed webhook
        Assert.True(PaymentLifecycleRules.IsDuplicateOrIdempotentWebhook("COMPLETED", "COMPLETED", 100_000m, 100_000m));
        Assert.True(PaymentLifecycleRules.IsDuplicateOrIdempotentWebhook("COMPLETED", "COMPLETED", 100_000m, 50_000m));

        // Canceled payment receiving duplicate cancel webhook
        Assert.True(PaymentLifecycleRules.IsDuplicateOrIdempotentWebhook("CANCELED", "CANCELED", 0m, 0m));

        // Pending payment receiving completed webhook -> NOT duplicate, must process
        Assert.False(PaymentLifecycleRules.IsDuplicateOrIdempotentWebhook("PENDING", "COMPLETED", 0m, 100_000m));
    }

    [Fact]
    public void PaymentRules_ValidateRefund_EnforcesPaidAndBalanceLimits()
    {
        // Valid partial refund
        var res1 = PaymentLifecycleRules.ValidateRefund(1_000_000m, 200_000m, 300_000m);
        Assert.True(res1.IsValid);
        Assert.Null(res1.ErrorMessage);
        Assert.Equal(500_000m, res1.RemainingRefundable);

        // Requested refund exceeds remaining balance
        var res2 = PaymentLifecycleRules.ValidateRefund(1_000_000m, 800_000m, 300_000m);
        Assert.False(res2.IsValid);
        Assert.Contains("vượt quá", res2.ErrorMessage);

        // Non-positive refund request
        var res3 = PaymentLifecycleRules.ValidateRefund(1_000_000m, 0m, 0m);
        Assert.False(res3.IsValid);
        Assert.Contains("lớn hơn 0", res3.ErrorMessage);

        // Original payment has zero or negative amount
        var res4 = PaymentLifecycleRules.ValidateRefund(0m, 0m, 100_000m);
        Assert.False(res4.IsValid);
        Assert.Contains("hợp lệ", res4.ErrorMessage);
    }

    // =========================================================================
    // SECTION 2: Invoice Pure Domain Rules (InvoiceLifecycleRules)
    // =========================================================================

    [Fact]
    public void InvoiceRules_NormalizeStatus_MapsAliases()
    {
        Assert.Equal("DRAFT", InvoiceLifecycleRules.NormalizeStatus("draft"));
        Assert.Equal("ISSUED", InvoiceLifecycleRules.NormalizeStatus("issued"));
        Assert.Equal("PAID", InvoiceLifecycleRules.NormalizeStatus("paid"));
        Assert.Equal("CANCELLED", InvoiceLifecycleRules.NormalizeStatus("CANCELED"));
        Assert.Equal("CANCELLED", InvoiceLifecycleRules.NormalizeStatus("cancelled"));
        Assert.Equal("DRAFT", InvoiceLifecycleRules.NormalizeStatus(null));
    }

    [Fact]
    public void InvoiceRules_IsImmutableStatus_EnforcesDocumentImmutability()
    {
        Assert.False(InvoiceLifecycleRules.IsImmutableStatus("DRAFT"));
        Assert.False(InvoiceLifecycleRules.IsImmutableStatus(null));

        Assert.True(InvoiceLifecycleRules.IsImmutableStatus("ISSUED"));
        Assert.True(InvoiceLifecycleRules.IsImmutableStatus("PAID"));
        Assert.True(InvoiceLifecycleRules.IsImmutableStatus("CANCELLED"));
        Assert.True(InvoiceLifecycleRules.IsImmutableStatus("CANCELED"));
    }

    [Fact]
    public void InvoiceRules_EnsureInvoiceCanBeMutated_ThrowsOnImmutableOrClosedOrder()
    {
        // Draft with open order -> passes without throwing
        InvoiceLifecycleRules.EnsureInvoiceCanBeMutated("DRAFT", isServiceOrderClosed: false);

        // Issued invoice -> throws
        var ex1 = Assert.Throws<InvalidOperationException>(() =>
            InvoiceLifecycleRules.EnsureInvoiceCanBeMutated("ISSUED", isServiceOrderClosed: false));
        Assert.Contains("chứng từ bất biến", ex1.Message);

        // Paid invoice -> throws
        var ex2 = Assert.Throws<InvalidOperationException>(() =>
            InvoiceLifecycleRules.EnsureInvoiceCanBeMutated("PAID", isServiceOrderClosed: false));
        Assert.Contains("chứng từ bất biến", ex2.Message);

        // Closed ServiceOrder -> throws
        var ex3 = Assert.Throws<InvalidOperationException>(() =>
            InvoiceLifecycleRules.EnsureInvoiceCanBeMutated("DRAFT", isServiceOrderClosed: true));
        Assert.Contains("liên kết đã đóng", ex3.Message);
    }

    [Fact]
    public void InvoiceRules_CanTransition_EnforcesDocumentWorkflow()
    {
        // Draft transitions
        Assert.True(InvoiceLifecycleRules.CanTransition("DRAFT", "ISSUED"));
        Assert.True(InvoiceLifecycleRules.CanTransition("DRAFT", "CANCELLED"));

        // Issued transitions
        Assert.True(InvoiceLifecycleRules.CanTransition("ISSUED", "PAID"));
        Assert.True(InvoiceLifecycleRules.CanTransition("ISSUED", "CANCELLED"));

        // Terminal states cannot transition
        Assert.False(InvoiceLifecycleRules.CanTransition("PAID", "DRAFT"));
        Assert.False(InvoiceLifecycleRules.CanTransition("PAID", "CANCELLED"));
        Assert.False(InvoiceLifecycleRules.CanTransition("CANCELLED", "ISSUED"));
    }

    [Fact]
    public void InvoiceRules_ResolvePaymentStatus_CalculatesCorrectly()
    {
        Assert.Equal("PAID", InvoiceLifecycleRules.ResolvePaymentStatus(0m, 0m));
        Assert.Equal("UNPAID", InvoiceLifecycleRules.ResolvePaymentStatus(0m, 500_000m));
        Assert.Equal("PARTIAL", InvoiceLifecycleRules.ResolvePaymentStatus(200_000m, 500_000m));
        Assert.Equal("PAID", InvoiceLifecycleRules.ResolvePaymentStatus(500_000m, 500_000m));
        Assert.Equal("PAID", InvoiceLifecycleRules.ResolvePaymentStatus(600_000m, 500_000m));
    }

    [Fact]
    public void InvoiceRules_CalculateLineAmounts_AndInvoiceTotals()
    {
        // Line 1: UnitPrice 100,000, Qty 2, TaxRate 10%
        var line1 = InvoiceLifecycleRules.CalculateLineAmounts(100_000m, 2m, 10m);
        Assert.Equal(200_000m, line1.SubTotal);
        Assert.Equal(20_000m, line1.TaxAmount);
        Assert.Equal(220_000m, line1.FinalAmount);

        // Line 2: UnitPrice 50,000, Qty 3, TaxRate 8% (0.08 format)
        var line2 = InvoiceLifecycleRules.CalculateLineAmounts(50_000m, 3m, 0.08m);
        Assert.Equal(150_000m, line2.SubTotal);
        Assert.Equal(12_000m, line2.TaxAmount);
        Assert.Equal(162_000m, line2.FinalAmount);

        // Totals with discount 32,000
        var totals = InvoiceLifecycleRules.CalculateInvoiceTotals(new[] { line1, line2 }, discountAmount: 32_000m);
        Assert.Equal(350_000m, totals.SubTotal);
        Assert.Equal(32_000m, totals.TaxAmount);
        Assert.Equal(32_000m, totals.DiscountAmount);
        Assert.Equal(350_000m, totals.FinalAmount);
    }

    [Fact]
    public void InvoiceRules_AllocatePaymentToInvoice_AllocatesAndComputesBalances()
    {
        // Total 1,000,000, already paid 200,000, incoming 500,000
        var res1 = InvoiceLifecycleRules.AllocatePaymentToInvoice(1_000_000m, 200_000m, 500_000m);
        Assert.Equal(500_000m, res1.AllocatedAmount);
        Assert.Equal(700_000m, res1.NewPaidAmount);
        Assert.Equal(300_000m, res1.RemainingDue);
        Assert.Equal("PARTIAL", res1.NewPaymentStatus);

        // Final payment 400,000 (overpayment allocated up to remaining)
        var res2 = InvoiceLifecycleRules.AllocatePaymentToInvoice(1_000_000m, 700_000m, 400_000m);
        Assert.Equal(300_000m, res2.AllocatedAmount);
        Assert.Equal(1_100_000m, res2.NewPaidAmount);
        Assert.Equal(0m, res2.RemainingDue);
        Assert.Equal("PAID", res2.NewPaymentStatus);
    }

    // =========================================================================
    // SECTION 3: Accounting Pure Domain Rules (AccountingDomainRules)
    // =========================================================================

    [Fact]
    public void AccountingRules_CheckEquilibrium_EnforcesTolerance()
    {
        // Exact balance
        var (b1, d1) = AccountingDomainRules.CheckEquilibrium(1_000_000m, 1_000_000m);
        Assert.True(b1);
        Assert.Equal(0m, d1);

        // Within 0.005 tolerance (e.g. 0.003 discrepancy)
        var (b2, d2) = AccountingDomainRules.CheckEquilibrium(1_000_000.003m, 1_000_000.000m);
        Assert.True(b2);
        Assert.True(d2 <= AccountingDomainRules.BalancingTolerance);

        // Outside 0.005 tolerance (e.g. 0.01 discrepancy)
        var (b3, d3) = AccountingDomainRules.CheckEquilibrium(1_000_000.01m, 1_000_000.00m);
        Assert.False(b3);
        Assert.Equal(0.01m, d3);
    }

    [Fact]
    public void AccountingRules_ValidateJournalPosting_RequiresDraftLinesAndEquilibrium()
    {
        // Valid journal
        var (canPost1, err1) = AccountingDomainRules.ValidateJournalPosting("DRAFT", 2, 500_000m, 500_000m);
        Assert.True(canPost1);
        Assert.Null(err1);

        // Non-draft status
        var (canPost2, err2) = AccountingDomainRules.ValidateJournalPosting("POSTED", 2, 500_000m, 500_000m);
        Assert.False(canPost2);
        Assert.Contains("Only DRAFT journal", err2);

        // Zero lines
        var (canPost3, err3) = AccountingDomainRules.ValidateJournalPosting("DRAFT", 0, 0m, 0m);
        Assert.False(canPost3);
        Assert.Contains("no lines", err3);

        // Unbalanced debit/credit
        var (canPost4, err4) = AccountingDomainRules.ValidateJournalPosting("DRAFT", 2, 500_000m, 400_000m);
        Assert.False(canPost4);
        Assert.Contains("must be balanced", err4);
    }

    [Fact]
    public void AccountingRules_RestrictedGenericWriteTables_GuardsAll27Tables()
    {
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("journal-entries"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("journal-entry-lines"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("chart-of-accounts"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("supplier-invoices"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("supplier-payments"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("bank-accounts"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("bank-transactions"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("payroll-runs"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("payslips"));
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("tax-filings"));

        // Case insensitivity
        Assert.True(AccountingDomainRules.IsTableRestrictedForGenericWrite("JOURNAL-ENTRIES"));

        // Safe/unrestricted tables return false
        Assert.False(AccountingDomainRules.IsTableRestrictedForGenericWrite("non-financial-table"));
        Assert.False(AccountingDomainRules.IsTableRestrictedForGenericWrite(null));
    }

    // =========================================================================
    // SECTION 4: Authorization Ports & Adapters Tests
    // =========================================================================

    [Fact]
    public async Task PaymentAuthorizationService_EnforcesOwnershipAndBranchScoping()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PaymentAuthDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);
        db.Branches.AddRange(
            new Branch { Id = 1, Name = "Branch 1", Status = "ACTIVE" },
            new Branch { Id = 2, Name = "Branch 2", Status = "ACTIVE" }
        );

        db.Customers.AddRange(
            new Customer { Id = 100, FullName = "Customer A", Phone = "0900000001", Type = "INDIVIDUAL", IsActive = true },
            new Customer { Id = 200, FullName = "Customer B", Phone = "0900000002", Type = "INDIVIDUAL", IsActive = true }
        );

        db.ServiceOrders.Add(new ServiceOrder
        {
            Id = 10,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            CustomerId = 100,
            Code = "SO-10",
            Status = "COMPLETED"
        });

        db.Payments.Add(new Payment
        {
            Id = 501,
            ServiceOrderId = 10,
            PayerType = "CUSTOMER",
            PayerId = 100,
            MethodId = 1,
            Amount = 500_000m,
            Direction = "INFLOW",
            Type = "PAYMENT",
            Status = "COMPLETED"
        });

        await db.SaveChangesAsync();

        var authService = new PaymentAuthorizationService(new EfPaymentStore(db));

        var customerA = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var customerB = new UserActorContext(200, "Customer", "CUSTOMER", null, false);
        var staffBranch1 = new UserActorContext(301, "Staff", "ACCOUNTANT", 1, false);
        var staffBranch2 = new UserActorContext(302, "Staff", "ACCOUNTANT", 2, false);
        var admin = new UserActorContext(999, "Staff", "ADMIN", null, true);

        // 1. Customer A can view their own payment; Customer B cannot
        Assert.True(await authService.CanViewPaymentAsync(501, customerA));
        Assert.False(await authService.CanViewPaymentAsync(501, customerB));

        // 2. Staff Branch 1 can view; Staff Branch 2 cannot
        Assert.True(await authService.CanViewPaymentAsync(501, staffBranch1));
        Assert.False(await authService.CanViewPaymentAsync(501, staffBranch2));

        // 3. Admin can view all
        Assert.True(await authService.CanViewPaymentAsync(501, admin));

        // 4. Creation: Customer cannot create offline payments; Staff with Accountant can
        Assert.False(await authService.CanCreatePaymentAsync(1, customerA));
        Assert.True(await authService.CanCreatePaymentAsync(1, staffBranch1));

        // 5. Transition: Accountant can transition; Customer cannot
        Assert.True(await authService.CanTransitionPaymentStatusAsync(501, staffBranch1));
        Assert.False(await authService.CanTransitionPaymentStatusAsync(501, customerA));

        // 6. Refund: Accountant at Branch 1 can refund; Branch 2 cannot
        Assert.True(await authService.CanRefundPaymentAsync(501, staffBranch1));
        Assert.False(await authService.CanRefundPaymentAsync(501, staffBranch2));
    }

    [Fact]
    public async Task InvoiceAndAccountingAuthorizationService_EnforcesRoleGating()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"InvoiceAuthDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);
        db.Branches.Add(new Branch { Id = 1, Name = "Branch 1", Status = "ACTIVE" });
        db.ServiceOrders.Add(new ServiceOrder
        {
            Id = 20,
            OriginBranchId = 1,
            ExecuteBranchId = 1,
            CustomerId = 100,
            Code = "SO-20",
            Status = "COMPLETED"
        });

        db.Invoices.Add(new Invoice
        {
            Id = 701,
            ServiceOrderId = 20,
            PayerType = "CUSTOMER",
            PayerId = 100,
            TotalAmount = 800_000m,
            PaymentStatus = "PAID"
        });

        await db.SaveChangesAsync();

        var invoiceAuth = new InvoiceAuthorizationService(new EfInvoiceStore(db));
        var accountingAuth = new AccountingAuthorizationService();

        var customer = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var otherCustomer = new UserActorContext(101, "Customer", "CUSTOMER", null, false);
        var accountant = new UserActorContext(300, "Staff", "ACCOUNTANT", 1, false);
        var mechanic = new UserActorContext(305, "Staff", "TECHNICIAN", 1, false);
        var admin = new UserActorContext(999, "Staff", "ADMIN", null, true);

        // Invoice Viewing
        Assert.True(await invoiceAuth.CanViewInvoiceAsync(701, customer));
        Assert.False(await invoiceAuth.CanViewInvoiceAsync(701, otherCustomer));
        Assert.True(await invoiceAuth.CanViewInvoiceAsync(701, accountant));
        Assert.True(await invoiceAuth.CanViewInvoiceAsync(701, admin));

        // Tax summary export
        Assert.True(await invoiceAuth.CanExportTaxSummaryAsync(accountant));
        Assert.True(await invoiceAuth.CanExportTaxSummaryAsync(admin));
        Assert.False(await invoiceAuth.CanExportTaxSummaryAsync(customer));
        Assert.False(await invoiceAuth.CanExportTaxSummaryAsync(mechanic));

        // Accounting Gating: Customer & Mechanic are forbidden
        Assert.False(await accountingAuth.CanAccessTableAsync("journal-entries", 1, customer));
        Assert.False(await accountingAuth.CanAccessTableAsync("journal-entries", 1, mechanic));
        Assert.True(await accountingAuth.CanAccessTableAsync("journal-entries", 1, accountant));
        Assert.True(await accountingAuth.CanAccessTableAsync("journal-entries", 1, admin));

        // Journal Posting Gating
        Assert.True(await accountingAuth.CanPostJournalEntryAsync(10, accountant));
        Assert.False(await accountingAuth.CanPostJournalEntryAsync(10, mechanic));
        Assert.False(await accountingAuth.CanPostJournalEntryAsync(10, customer));
    }

    // =========================================================================
    // SECTION 5: Application Services Integration Tests
    // =========================================================================

    [Fact]
    public async Task PaymentAndInvoiceApplicationServices_ExecuteWithStrategyTransactions()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"AppServicesDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);
        db.Branches.Add(new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" });
        db.PaymentMethods.Add(new PaymentMethod { Id = 1, MethodCode = "CASH", MethodName = "Tiền mặt", IsActive = true });
        db.Products.Add(new Product { Id = 10, Sku = "OIL-5W30", Unit = "Chai", Name = "Dầu nhớt", RetailPrice = 300_000m, TaxRate = 10m, IsActive = true });
        await db.SaveChangesAsync();

        var paymentAuth = new PaymentAuthorizationService(new EfPaymentStore(db));
        var paymentApp = new MyGarage.Application.Payments.PaymentApplicationService(
            new MyGarage.Infrastructure.Payments.EfPaymentStore(db),
            paymentAuth);

        var invoiceAuth = new InvoiceAuthorizationService(new EfInvoiceStore(db));
        var invoiceApp = new MyGarage.Application.Invoices.InvoiceApplicationService(
            new MyGarage.Infrastructure.Invoices.EfInvoiceStore(db),
            invoiceAuth);

        var staffActor = new UserActorContext(300, "Staff", "ACCOUNTANT", 1, false);

        // 1. Create Invoice via InvoiceApplicationService
        var invoiceInput = new InvoiceCreateInput(
            ServiceOrderId: null,
            RetailOrderId: null,
            VehicleId: null,
            Phone: "0901234567",
            Address: "123 Đường Test",
            CompanyName: null,
            TaxCode: null,
            PayerType: "CUSTOMER",
            PayerId: 100,
            Details: new List<InvoiceDetailInput>
            {
                new(ProductId: 10, StandardServiceId: null, ItemType: "PART", ItemName: "Dầu nhớt", Quantity: 2m, UnitPrice: 300_000m, TaxRatePercent: 10m)
            }
        );

        var createdInvoice = await invoiceApp.CreateInvoiceAsync(invoiceInput, staffActor);
        Assert.NotNull(createdInvoice);
        Assert.True(createdInvoice.Id > 0);
        Assert.Equal(600_000m, createdInvoice.SubTotalAmount);
        Assert.Equal(60_000m, createdInvoice.TaxAmount);
        Assert.Equal(660_000m, createdInvoice.TotalAmount);
        Assert.Equal("UNPAID", createdInvoice.PaymentStatus);

        // 2. Calculate Totals on created invoice
        var calcResult = await invoiceApp.CalculateTotalsAsync(createdInvoice.Id);
        Assert.Equal(660_000m, calcResult.TotalAmount);
        Assert.Equal(660_000m, calcResult.RemainingDue);
        Assert.Equal("UNPAID", calcResult.PaymentStatus);

        // 3. Create Payment via PaymentApplicationService anchored to Invoice
        var paymentInput = new PaymentCreateInput(
            InvoiceId: createdInvoice.Id,
            ServiceOrderId: null,
            RetailOrderId: null,
            PayerType: "CUSTOMER",
            PayerId: 100,
            MethodId: 1,
            Amount: 660_000m,
            TransactionCode: "TX-TEST-001",
            Type: "PAYMENT",
            Status: "PENDING",
            Direction: null // should normalize to INFLOW because InvoiceId > 0
        );

        var createdPayment = await paymentApp.CreatePaymentAsync(paymentInput, staffActor);
        Assert.NotNull(createdPayment);
        Assert.True(createdPayment.Id > 0);
        Assert.Equal("INFLOW", createdPayment.Direction);
        Assert.Equal("PENDING", createdPayment.Status);

        // 4. Transition Payment status to COMPLETED
        var transitionInput = new PaymentTransitionInput("COMPLETED", 660_000m, "Paid in full");
        var transitionedPayment = await paymentApp.TransitionStatusAsync(createdPayment.Id, transitionInput, staffActor);
        Assert.Equal("COMPLETED", transitionedPayment.Status);

        // 5. Invalid transition from COMPLETED to PENDING throws InvalidOperationException
        var invalidInput = new PaymentTransitionInput("PENDING", null, "Invalid back-transition");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paymentApp.TransitionStatusAsync(createdPayment.Id, invalidInput, staffActor));
    }

    [Fact]
    public async Task DashboardV2_DoesNotReportUnimplementedMetricsAsZero()
    {
        var service = new AccountingFinanceWorkflowService(new FakeAccountingCrudService());
        var result = await service.GetDashboardV2Async(null, null, 30, 1);
        var payload = JsonSerializer.SerializeToElement(result.Payload);
        Assert.False(payload.GetProperty("detailsAvailable").GetBoolean());
        var summary = payload.GetProperty("summary");
        foreach (var field in new[] { "totalOutstandingAr", "totalOutstandingAp", "overdueArCount", "overdueApAmount", "pendingApprovalCount" })
            Assert.Equal(JsonValueKind.Null, summary.GetProperty(field).ValueKind);

        var file = await service.ExportDashboardV2CsvAsync(null, null, 30, 1);
        var csv = System.Text.Encoding.UTF8.GetString(file.Content);
        Assert.Contains("meta,detailsAvailable,false", csv);
        Assert.Contains("summary,totalOutstandingAr," + Environment.NewLine, csv);
        Assert.DoesNotContain("summary,totalOutstandingAr,0", csv);
    }

    [Theory]
    [InlineData("trial")]
    [InlineData("payable")]
    [InlineData("payroll")]
    public async Task AccountingReports_RequireBranchColumnForScopedView(string report)
    {
        var crud = new FakeAccountingCrudService();
        var service = new AccountingFinanceWorkflowService(crud);
        Task<AccountingCommandResult> GetReport(long? branchId) => report switch
        {
            "trial" => service.GetTrialBalanceAsync(branchId),
            "payable" => service.GetSupplierPayableSummaryAsync(branchId),
            _ => service.GetPayrollRunSummaryAsync(branchId)
        };

        Assert.Equal(503, (await GetReport(5)).StatusCode);
        Assert.Null(crud.LastQuery);
        Assert.Equal(200, (await GetReport(null)).StatusCode);
        Assert.DoesNotContain("branch_id=@branchId", crud.LastQuery);
        crud.HasBranchColumn = true;
        Assert.Equal(200, (await GetReport(5)).StatusCode);
        Assert.Contains("@branchId IS NULL OR branch_id=@branchId", crud.LastQuery);
        Assert.Equal(200, (await GetReport(null)).StatusCode);
    }

    private sealed class FakeAccountingCrudService : IAccountingCrudService
    {
        public bool HasBranchColumn { get; set; }
        public string? LastQuery { get; private set; }
        public readonly Dictionary<long, Dictionary<string, object?>> JournalEntries = new();
        public readonly List<Dictionary<string, object?>> JournalLines = new();

        public bool TryGetConfig(string key, out AccountingCrudTableConfig config)
        {
            config = new AccountingCrudTableConfig("dbo." + key.Replace("-", "_"), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id" });
            return true;
        }

        public Task<bool> TableExistsAsync(AccountingCrudTableConfig config) => Task.FromResult(true);
        public Task<bool> ViewExistsAsync(string schemaViewName) => Task.FromResult(true);
        public Task<IReadOnlyList<Dictionary<string, object?>>> GetAllAsync(string tableKey, int limit = 500, long? branchId = null) =>
            Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(new List<Dictionary<string, object?>>());

        public Task<Dictionary<string, object?>?> GetByIdAsync(string tableKey, long id, long? branchId = null)
        {
            if (tableKey == "journal-entries" && JournalEntries.TryGetValue(id, out var row))
            {
                return Task.FromResult<Dictionary<string, object?>?>(row);
            }
            return Task.FromResult<Dictionary<string, object?>?>(null);
        }

        public Task<Dictionary<string, object?>?> InsertAsync(string tableKey, JsonElement payload, long? branchId = null) =>
            Task.FromResult<Dictionary<string, object?>?>(null);

        public Task<Dictionary<string, object?>?> UpdateAsync(string tableKey, long id, JsonElement payload, long? branchId = null) =>
            Task.FromResult<Dictionary<string, object?>?>(null);

        public Task<bool> DeleteAsync(string tableKey, long id, long? branchId = null) => Task.FromResult(true);

        public Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, IEnumerable<QueryParam>? parameters = null)
        {
            LastQuery = sql;
            var pList = parameters?.ToList() ?? new List<QueryParam>();
            var idParam = pList.FirstOrDefault(p => p.Name == "@id");
            long id = idParam?.Value != null ? Convert.ToInt64(idParam.Value) : 0;

            var matchingLines = JournalLines
                .Where(l => l.TryGetValue("journal_entry_id", out var jId) && Convert.ToInt64(jId) == id)
                .ToList();

            var totalDebit = matchingLines.Sum(l => l.TryGetValue("debit_amount", out var d) ? Convert.ToDecimal(d) : 0m);
            var totalCredit = matchingLines.Sum(l => l.TryGetValue("credit_amount", out var c) ? Convert.ToDecimal(c) : 0m);
            var count = matchingLines.Count;

            var result = new Dictionary<string, object?>
            {
                ["total_debit"] = totalDebit,
                ["total_credit"] = totalCredit,
                ["line_count"] = count,
                ["totalDebit"] = totalDebit,
                ["totalCredit"] = totalCredit,
                ["lineCount"] = count
            };

            return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(new List<Dictionary<string, object?>> { result });
        }

        public Task<int> ExecuteAsync(string sql, IEnumerable<QueryParam>? parameters = null)
        {
            var pList = parameters?.ToList() ?? new List<QueryParam>();
            var idParam = pList.FirstOrDefault(p => p.Name == "@id");
            var statusParam = pList.FirstOrDefault(p => p.Name == "@status");
            var userParam = pList.FirstOrDefault(p => p.Name == "@u");

            if (idParam?.Value != null)
            {
                var id = Convert.ToInt64(idParam.Value);
                if (JournalEntries.TryGetValue(id, out var entry))
                {
                    if (statusParam?.Value != null) entry["status"] = statusParam.Value.ToString();
                    if (userParam?.Value != null) entry["posted_by"] = Convert.ToInt64(userParam.Value);
                    entry["posted_at"] = DateTime.UtcNow;
                    return Task.FromResult(1);
                }
            }
            return Task.FromResult(0);
        }

        public Task<T?> ScalarAsync<T>(string sql, IEnumerable<QueryParam>? parameters = null) =>
            Task.FromResult(sql.Contains("COL_LENGTH") ? (T?)(object)(HasBranchColumn ? 1 : 0) : default);
    }

    [Fact]
    public async Task AccountingApplicationService_ValidatesAndPostsJournalEntries()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"AccountingAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);
        db.Branches.Add(new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" });
        await db.SaveChangesAsync();

        var fakeCrud = new FakeAccountingCrudService();
        fakeCrud.JournalEntries[50] = new Dictionary<string, object?>
        {
            ["id"] = 50L,
            ["entry_no"] = "JE-2026-001",
            ["status"] = "DRAFT",
            ["posted_by"] = null,
            ["posted_at"] = null
        };
        fakeCrud.JournalLines.Add(new Dictionary<string, object?>
        {
            ["id"] = 1L,
            ["journal_entry_id"] = 50L,
            ["debit_amount"] = 500_000m,
            ["credit_amount"] = 0m
        });
        fakeCrud.JournalLines.Add(new Dictionary<string, object?>
        {
            ["id"] = 2L,
            ["journal_entry_id"] = 50L,
            ["debit_amount"] = 0m,
            ["credit_amount"] = 500_000m
        });

        var authService = new AccountingAuthorizationService();
        var appService = new MyGarage.Application.Accounting.AccountingApplicationService(
            new MyGarage.Infrastructure.Accounting.EfAccountingStore(fakeCrud, fakeCrud, fakeCrud),
            authService);
        var actor = new UserActorContext(300, "Staff", "ACCOUNTANT", 1, false);

        // 1. Validate equilibrium
        var eqResult = await appService.ValidateJournalEquilibriumAsync(50);
        Assert.True(eqResult.IsBalanced);
        Assert.Equal(500_000m, eqResult.TotalDebit);
        Assert.Equal(500_000m, eqResult.TotalCredit);
        Assert.Equal(0m, eqResult.Discrepancy);
        Assert.Equal(2, eqResult.LineCount);

        // 2. Post journal entry
        var postResult = await appService.PostJournalEntryAsync(50, new PostJournalInput("Month end closing"), actor);
        Assert.Equal("POSTED", postResult.Status);
        Assert.Equal(300, postResult.PostedBy);

        // 3. Trying to post already POSTED journal throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appService.PostJournalEntryAsync(50, new PostJournalInput("Re-post"), actor));

        // 4. Restricted tables check
        Assert.True(await appService.IsTableRestrictedAsync("journal-entries"));
        Assert.False(await appService.IsTableRestrictedAsync("safe-table"));
    }

    // =========================================================================
    // SECTION 6: Dual Route API Endpoints Integration Tests (All 13 Controllers)
    // =========================================================================

    [Fact]
    public async Task DualRoutes_AllModule7Controllers_RespondSuccessfully()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            db.PaymentMethods.Add(new PaymentMethod { Id = 1, MethodCode = "CASH", MethodName = "Tiền mặt", IsActive = true });

            db.Invoices.Add(new Invoice
            {
                Id = 1,
                PayerType = "CUSTOMER",
                PayerId = 100,
                TotalAmount = 500_000m,
                PaymentStatus = "UNPAID",
                CreatedAt = DateTime.UtcNow
            });

            db.InvoiceDetails.Add(new InvoiceDetail
            {
                Id = 1,
                InvoiceId = 1,
                ItemType = "PART",
                ItemName = "Bugi",
                Quantity = 4m,
                UnitPriceAtTime = 125_000m,
                Unit = "Cái",
                SubTotal = 500_000m,
                FinalAmount = 500_000m
            });

            db.Payments.Add(new Payment
            {
                Id = 1,
                InvoiceId = 1,
                PayerType = "CUSTOMER",
                PayerId = 100,
                MethodId = 1,
                Amount = 500_000m,
                Direction = "INFLOW",
                Type = "PAYMENT",
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            });

            db.ExpenseLogs.Add(new ExpenseLog
            {
                Id = 1,
                BranchId = 1,
                ExpenseType = "OFFICE",
                Amount = 200_000m,
                Description = "Mua nước uống văn phòng",
                Status = "APPROVED",
                CreatedAt = DateTime.UtcNow
            });

            db.RevenueSnapshots.Add(new RevenueSnapshot
            {
                Id = 1,
                BranchId = 1,
                SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
                TotalRevenue = 10_000_000m,
                CreatedAt = DateTime.UtcNow
            });

            db.ServiceRefunds.Add(new ServiceRefund
            {
                Id = 1,
                ServiceOrderId = 1,
                RefundAmount = 150_000m,
                Reason = "Hoàn tiền công phụ tùng",
                Status = "PENDING",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "ADMIN", 1));

        // 1. OrderController (/api/v1/orders)
        var resOrder = await client.GetAsync("/api/v1/orders/999");
        Assert.Equal(HttpStatusCode.NotFound, resOrder.StatusCode);

        // 2. WebhookController (/api/v1/webhooks/payment)
        var webhookPayload = new
        {
            code = "00",
            desc = "success",
            data = new
            {
                orderCode = 123,
                amount = 2000,
                description = "VQRIO123",
                accountNumber = "12345678",
                reference = "TEST_REF",
                transactionDateTime = "2026-11-13 18:00:00",
                paymentLinkId = "link_123"
            },
            signature = "any"
        };
        var resWh = await client.PostAsJsonAsync("/api/v1/webhooks/payment", webhookPayload);
        Assert.NotEqual(HttpStatusCode.NotFound, resWh.StatusCode);

        // 3. InvoiceController (/api/v1/invoices)
        var resInv = await client.GetAsync("/api/v1/invoices");
        Assert.Equal(HttpStatusCode.OK, resInv.StatusCode);

        // Immutability invariant: PUT & DELETE return 405 Method Not Allowed
        var resInvPut = await client.PutAsJsonAsync("/api/v1/invoices/1", new { });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resInvPut.StatusCode);

        // 4. InvoiceListPageController (/api/v1/invoices/list-page)
        var resInvList = await client.GetAsync("/api/v1/invoices/list-page");
        Assert.Equal(HttpStatusCode.OK, resInvList.StatusCode);

        // 5. InvoiceTaxSummaryController (/api/v1/invoices/tax-summary)
        var resTax = await client.GetAsync("/api/v1/invoices/tax-summary");
        Assert.Equal(HttpStatusCode.OK, resTax.StatusCode);

        // 6. InvoiceViewDetailController (/api/v1/invoices/1/view-detail)
        var resInvView = await client.GetAsync("/api/v1/invoices/1/view-detail");
        Assert.Equal(HttpStatusCode.OK, resInvView.StatusCode);

        // 7. InvoiceDetailController (/api/v1/invoice-details)
        var resInvDet = await client.GetAsync("/api/v1/invoice-details");
        Assert.Equal(HttpStatusCode.OK, resInvDet.StatusCode);

        // 8. PaymentController (/api/v1/payments)
        var resPay = await client.GetAsync("/api/v1/payments");
        Assert.Equal(HttpStatusCode.OK, resPay.StatusCode);

        // 9. PaymentMethodController (/api/v1/payment-methods)
        var resPm = await client.GetAsync("/api/v1/payment-methods");
        Assert.Equal(HttpStatusCode.OK, resPm.StatusCode);

        // 10. AccountingController (/api/v1/accounting)
        // Generic write to restricted table returns 405 Method Not Allowed
        var resAcc = await client.PostAsJsonAsync("/api/v1/accounting/table/journal-entries", new { });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resAcc.StatusCode);

        // 11. ExpenseLogController (/api/v1/expense-logs)
        var resExp = await client.GetAsync("/api/v1/expense-logs");
        Assert.Equal(HttpStatusCode.OK, resExp.StatusCode);

        // 12. RevenueSnapshotController (/api/v1/revenue-snapshots)
        var resRev = await client.GetAsync("/api/v1/revenue-snapshots");
        Assert.Equal(HttpStatusCode.OK, resRev.StatusCode);

        // 13. ServiceRefundController (/api/v1/service-refunds)
        var resRef = await client.GetAsync("/api/v1/service-refunds");
        Assert.Equal(HttpStatusCode.OK, resRef.StatusCode);
    }
}
