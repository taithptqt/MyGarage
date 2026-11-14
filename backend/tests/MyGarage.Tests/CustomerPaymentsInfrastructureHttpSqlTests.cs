using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Payments;
using MyGarage.Infrastructure.Payments;
using Xunit;

namespace MyGarage.Tests;

public sealed class CustomerPaymentsInfrastructureHttpSqlTests
{
    [LocalSqlFact]
    public async Task Mine_UsesRegisteredInfrastructureReaderAndKeepsCustomerScope()
    {
        var name = "MyGaragePaymentsHttp_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            await using (var targetConnection = new SqlConnection(target.ConnectionString))
            {
                await targetConnection.OpenAsync();
                await using var seed = targetConnection.CreateCommand();
                seed.CommandText = """
                    CREATE TABLE dbo.Payments (Id bigint NOT NULL PRIMARY KEY, ShiftClosingId bigint NULL, CashierId bigint NULL,
                        ServiceOrderId bigint NULL, InvoiceId bigint NULL, RetailOrderId bigint NULL, PayerType varchar(50) NULL,
                        PayerId bigint NULL, MethodId bigint NOT NULL, Amount decimal(18,2) NOT NULL, TransactionCode varchar(100) NULL,
                        Type varchar(50) NOT NULL, Status varchar(50) NULL, ReversedPaymentId bigint NULL, CreatedAt datetime2 NULL,
                        UpdatedAt datetime2 NULL, UpdatedBy bigint NULL);
                    INSERT INTO dbo.Payments (Id, PayerType, PayerId, MethodId, Amount, Type, CreatedAt) VALUES
                        (1, 'CUSTOMER', 100, 1, 11, 'PAYMENT', '2026-01-01'),
                        (2, 'customer', 100, 1, 22, 'PAYMENT', '2026-01-01'),
                        (3, 'CUSTOMER', 101, 1, 33, 'PAYMENT', '2026-01-01'),
                        (4, 'STAFF', 100, 1, 44, 'PAYMENT', '2026-01-01');
                    """;
                await seed.ExecuteNonQueryAsync();
            }

            await using var factory = new TestWebApplicationFactory();
            await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
            using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICustomerPaymentsReader>();
                services.AddScoped<ICustomerPaymentsReader, CustomerPaymentsReader>();
                services.RemoveAll<DbContextOptions<PaymentReadDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PaymentReadDbContext>>();
                services.RemoveAll<PaymentReadDbContext>();
                services.AddDbContext<PaymentReadDbContext>(options => options.UseSqlServer(target.ConnectionString));
            }));
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
            using var response = await client.GetAsync("/api/payments/mine?page=2&pageSize=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(response.Headers.Contains("X-Total-Count"));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
            var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(1L, item.GetProperty("id").GetInt64());
            Assert.Equal(11m, item.GetProperty("amount").GetDecimal());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
