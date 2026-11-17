using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Data;
using MyGarage.Infrastructure.Invoices;
using MyGarage.Infrastructure.Payments;
using MyGarage.Models;
using MyGarage.Security;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceTransactionStartSqlTests
{
    [LocalSqlFact]
    public async Task FailedPaymentTransactionStart_IsPropagatedWithoutWriting()
    {
        var name = "MyGaragePaymentTxStart_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            await using (var setup = new SqlConnection(target.ConnectionString))
            {
                await setup.OpenAsync();
                await using var command = setup.CreateCommand();
                command.CommandText = "CREATE TABLE dbo.Payments (Id bigint NOT NULL PRIMARY KEY)";
                await command.ExecuteNonQueryAsync();
            }
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString)
                .AddInterceptors(new RejectTransactionInterceptor()).Options;
            await using var db = new GarageContext(options, new HttpAuditContextAccessor(new HttpContextAccessor()));
            var store = new EfPaymentStore(db);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.AddPaymentAsync(new Payment { Id = 123, PayerType = "CUSTOMER", Type = "PAYMENT" }));
            Assert.Contains("SqlServerRetryingExecutionStrategy simulated", exception.Message);
            await using var verify = new SqlConnection(target.ConnectionString);
            await verify.OpenAsync();
            await using var count = verify.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM dbo.Payments";
            Assert.Equal(0, (int)(await count.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [LocalSqlFact]
    public async Task FailedTransactionStart_IsPropagatedWithoutWritingInvoice()
    {
        var name = "MyGarageInvoiceTxStart_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            await using (var setup = new SqlConnection(target.ConnectionString))
            {
                await setup.OpenAsync();
                await using var command = setup.CreateCommand();
                command.CommandText = "CREATE TABLE dbo.Invoices (Id bigint NOT NULL PRIMARY KEY)";
                await command.ExecuteNonQueryAsync();
            }
            var options = new DbContextOptionsBuilder<GarageContext>()
                .UseSqlServer(target.ConnectionString)
                .AddInterceptors(new RejectTransactionInterceptor())
                .Options;
            await using var db = new GarageContext(options, new HttpAuditContextAccessor(new HttpContextAccessor()));
            var store = new EfInvoiceStore(db);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.CreateInvoiceWithDetailsAsync(new Invoice { Id = 123, PayerType = "CUSTOMER" }, Array.Empty<InvoiceDetail>()));
            Assert.Contains("SqlServerRetryingExecutionStrategy simulated", exception.Message);
            await using var verify = new SqlConnection(target.ConnectionString);
            await verify.OpenAsync();
            await using var count = verify.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM dbo.Invoices";
            Assert.Equal(0, (int)(await count.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class RejectTransactionInterceptor : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SqlServerRetryingExecutionStrategy simulated transaction start failure");
    }
}
