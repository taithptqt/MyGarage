using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Infrastructure.Payments;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class PaymentSettlementSqlTests
{
    [LocalSqlFact]
    public async Task FailedInvoiceLink_RollsBackCompletedPayment()
    {
        var name = "MyGarageSettlement_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            long paymentId;
            await using (var setup = new GarageContext(options))
            {
                var script = setup.Database.GenerateCreateScript();
                script = Regex.Replace(script, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
                script = script.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
                script = Regex.Replace(script, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
                foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    if (!string.IsNullOrWhiteSpace(batch)) await setup.Database.ExecuteSqlRawAsync(batch);
                await setup.Database.ExecuteSqlRawAsync("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
                var payment = new Payment { MethodId = 1, ServiceOrderId = 700, PayerType = "CUSTOMER", PayerId = 100, Type = "FULL", Direction = "OUTFLOW", Status = "PENDING", Amount = 10 };
                setup.Payments.Add(payment);
                await setup.SaveChangesAsync();
                paymentId = payment.Id;
            }
            await using (var db = new GarageContext(options))
            {
                var store = new EfPaymentStore(db);
                var invoice = DispatchProxy.Create<IInvoiceService, InvoiceFailureProxy>();
                var email = DispatchProxy.Create<IEmailService, NoEmailProxy>();
                var service = new PaymentConfirmationService(store, invoice, email);
                var payment = await db.Payments.SingleAsync(x => x.Id == paymentId);
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyOutcomeAsync(payment, 25, "COMPLETED"));
                Assert.Equal("simulated invoice failure", error.Message);
            }
            await using var verify = new GarageContext(options);
            var persisted = await verify.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId);
            Assert.Equal("PENDING", persisted.Status);
            Assert.Equal(10, persisted.Amount);
            Assert.Null(persisted.InvoiceId);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    public class InvoiceFailureProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IInvoiceService.EnsureForPaymentContextAsync))
                return Task.FromException<Invoice?>(new InvalidOperationException("simulated invoice failure"));
            throw new NotSupportedException(targetMethod.Name);
        }
    }
    public class NoEmailProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException(targetMethod!.Name);
    }
}