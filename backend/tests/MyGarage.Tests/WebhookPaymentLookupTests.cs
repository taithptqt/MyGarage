using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Infrastructure.Queries;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class WebhookPaymentLookupTests
{
    [Fact]
    public async Task MissingIdentifiers_DoNotMatchNullTransactionCode()
    {
        await using var db = CreateContext();
        db.Payments.Add(new Payment { Id = 1, Type = "FULL", Direction = "INFLOW" });
        await db.SaveChangesAsync();
        var service = new EfWebhookPaymentDataService(db, null!);

        Assert.Null(await service.FindPaymentForOrderAsync(null, null, " "));
        Assert.Null(await service.FindPaymentForOrderAsync(null, "missing-link", null));
    }

    [Fact]
    public async Task AmbiguousIdentifiers_RequireReconciliation()
    {
        await using var db = CreateContext();
        db.Payments.AddRange(
            new Payment { Id = 1, Type = "FULL", Direction = "INFLOW", TransactionCode = "link" },
            new Payment { Id = 2, Type = "FULL", Direction = "INFLOW", TransactionCode = "link" });
        await db.SaveChangesAsync();
        var service = new EfWebhookPaymentDataService(db, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FindPaymentForOrderAsync(null, "link", null));
    }

    private static GarageContext CreateContext() => new(
        new DbContextOptionsBuilder<GarageContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
