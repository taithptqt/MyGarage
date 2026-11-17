using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class PaymentExplicitInvoiceLinkTests
{
    [Fact]
    public async Task CompletedPayment_PreservesVerifiedInvoice_WhenNewerInvoiceHasDifferentPayer()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Invoices.AddRange(
                new Invoice { Id = 401, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100 },
                new Invoice { Id = 402, ServiceOrderId = 700, PayerType = "INSURANCE", PayerId = 300 });
            db.Payments.Add(new Payment
            {
                Id = 501, ServiceOrderId = 700, InvoiceId = 401, PayerType = "CUSTOMER", PayerId = 100,
                MethodId = 1, Type = "SERVICE", Status = "COMPLETED", Direction = "INFLOW", Amount = 10
            });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var payment = await db.Payments.SingleAsync(p => p.Id == 501);
        var service = scope.ServiceProvider.GetRequiredService<IPaymentConfirmationService>();
        await service.EnsureInvoiceLinkedAsync(payment);
        Assert.Equal(401, payment.InvoiceId);
        Assert.Equal("INFLOW", payment.Direction);
        Assert.Equal(2, await db.Invoices.CountAsync());
    }
    [Fact]
    public async Task CompletedPayment_WithMismatchedExplicitInvoice_IsNotReplaced()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Invoices.AddRange(
                new Invoice { Id = 401, ServiceOrderId = 700, PayerType = "INSURANCE", PayerId = 300 },
                new Invoice { Id = 402, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100 });
            db.Payments.Add(new Payment { Id = 501, ServiceOrderId = 700, InvoiceId = 401, PayerType = "CUSTOMER", PayerId = 100, MethodId = 1, Type = "SERVICE", Status = "COMPLETED", Direction = "INFLOW", Amount = 10 });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var payment = await db.Payments.SingleAsync(p => p.Id == 501);
        var service = scope.ServiceProvider.GetRequiredService<IPaymentConfirmationService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureInvoiceLinkedAsync(payment));
        Assert.Equal(401, payment.InvoiceId);
    }
}
