using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Data;
using MyGarage.Infrastructure.Persistence.Stores.Workshop;
using Xunit;

namespace MyGarage.Tests;

public sealed class DiagnosisApprovalTransactionSqlTests
{
    [LocalSqlFact]
    public async Task ApprovalTransaction_RollsBackFailureAndSerializesCompetingApprovals()
    {
        var name = "MyGarageApproval_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var setup = new GarageContext(options);
            await setup.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.ServiceOrders (Id bigint PRIMARY KEY); CREATE TABLE dbo.Decisions (Id int PRIMARY KEY); INSERT INTO dbo.ServiceOrders VALUES (1);");
            EfQuotationWorkflowStore Store(GarageContext db) => new(db, new ConfigurationBuilder().Build(), NullLogger<EfQuotationWorkflowStore>.Instance);
            await using (var db = new GarageContext(options))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Store(db).ExecuteDiagnosisApprovalTransactionalAsync<int>(1, async () =>
                {
                    await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Decisions VALUES (1)");
                    throw new InvalidOperationException("quotation failed");
                }));
            }
            Assert.Equal(0, await setup.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Decisions").SingleAsync());

            async Task<int> Approve()
            {
                await using var db = new GarageContext(options);
                return await Store(db).ExecuteDiagnosisApprovalTransactionalAsync(1, async () =>
                {
                    var count = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Decisions").SingleAsync();
                    if (count != 0) return 0;
                    await Task.Delay(100);
                    await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.Decisions VALUES (1)");
                    return 1;
                });
            }
            var results = await Task.WhenAll(Approve(), Approve());
            Assert.Equal(1, results.Sum());
            Assert.Equal(1, await setup.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Decisions").SingleAsync());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
