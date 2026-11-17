using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Invoices;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceBackfillLinkTests
{
    [Fact]
    public async Task Backfill_DoesNotReplaceExistingInvoiceLink()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Invoices.AddRange(
                new Invoice { Id = 801, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100 },
                new Invoice { Id = 802, ServiceOrderId = 700, PayerType = "INSURANCE", PayerId = 300 });
            db.Payments.Add(new Payment
            {
                Id = 803, ServiceOrderId = 700, InvoiceId = 801, PayerType = "CUSTOMER", PayerId = 100,
                MethodId = 1, Type = "SERVICE", Status = "COMPLETED", Direction = "INFLOW", Amount = 10
            });
            return Task.CompletedTask;
        });
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var result = await service.BackfillCompletedPaymentsAsync();
        Assert.Equal(1, result.CompletedPaymentsScanned);
        Assert.Equal(0, result.LinkedPayments);
        var store = scope.ServiceProvider.GetRequiredService<IInvoiceBackfillStore>();
        Assert.Equal(801, Assert.Single(await store.GetCompletedPaymentsForBackfillAsync()).InvoiceId);
    }
}
