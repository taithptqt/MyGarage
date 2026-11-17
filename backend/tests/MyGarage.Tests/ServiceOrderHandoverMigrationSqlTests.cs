using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyGarage.Data;
using Xunit;

namespace MyGarage.Tests;

public sealed class ServiceOrderHandoverMigrationSqlTests
{
    [LocalSqlFact]
    public async Task Migration_AddsNullableHandoverDetails_WithoutChangingExistingRows()
    {
        var name = "MyGarageHandover_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new GarageContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.ServiceOrders (Id bigint NOT NULL PRIMARY KEY); INSERT dbo.ServiceOrders(Id) VALUES (1);");
            var assembly = db.GetService<IMigrationsAssembly>();
            var migrationType = assembly.Migrations["20261116180000_AddServiceOrderHandoverDetails"];
            var migration = assembly.CreateMigration(migrationType, db.Database.ProviderName!);
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model);
            foreach (var command in commands) await db.Database.ExecuteSqlRawAsync(command.CommandText);
            await using var testConnection = new SqlConnection(target.ConnectionString);
            await testConnection.OpenAsync();
            await using (var legacyQuery = testConnection.CreateCommand())
            {
                legacyQuery.CommandText = "SELECT COUNT(*) FROM dbo.ServiceOrders WHERE Id=1 AND HandoverReceiverName IS NULL AND HandoverReceiverPhone IS NULL AND HandoverNotes IS NULL";
                Assert.Equal(1, (int)(await legacyQuery.ExecuteScalarAsync())!);
            }
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.ServiceOrders SET HandoverReceiverName=N'Nguyễn Văn A', HandoverReceiverPhone=N'0901234567', HandoverNotes=N'Đã ký' WHERE Id=1");
            await using var query = testConnection.CreateCommand();
            query.CommandText = "SELECT HandoverReceiverName, HandoverReceiverPhone, HandoverNotes FROM dbo.ServiceOrders WHERE Id=1";
            await using var reader = await query.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Nguyễn Văn A", reader.GetString(0));
            Assert.Equal("0901234567", reader.GetString(1));
            Assert.Equal("Đã ký", reader.GetString(2));
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}