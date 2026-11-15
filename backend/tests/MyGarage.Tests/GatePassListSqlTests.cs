using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Infrastructure.GatePasses;
using Xunit;

namespace MyGarage.Tests;

public sealed class GatePassListSqlTests
{
    [LocalSqlFact]
    public async Task Reader_UsesExistingTableAndRejectsWrites()
    {
        var name = "GatePassReadTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            await using var sql = new SqlConnection(test.ConnectionString);
            await sql.OpenAsync();
            await using (var command = sql.CreateCommand())
            {
                command.CommandText = """
                    CREATE TABLE dbo.GatePass (Id bigint NOT NULL PRIMARY KEY, ServiceOrderId bigint NOT NULL,
                        ClearanceType varchar(50) NOT NULL, QrCode varchar(100) NOT NULL, IssuedBy bigint NOT NULL,
                        ApprovedBy bigint NULL, ResolutionNotes nvarchar(max) NULL, OldPartsPackingListUrl varchar(500) NULL,
                        IsOldPartsChecked bit NULL, Status varchar(50) NOT NULL, ScannedBy bigint NULL,
                        SecurityCheckedAt datetime2 NULL, PassedAt datetime2 NULL);
                    INSERT INTO dbo.GatePass (Id, ServiceOrderId, ClearanceType, QrCode, IssuedBy, Status)
                        VALUES (1, 11, 'NORMAL', 'QR1', 200, 'ISSUED'), (2, 12, 'NORMAL', 'QR2', 200, 'ISSUED');
                    """;
                await command.ExecuteNonQueryAsync();
            }
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var db = new GarageContext(options);
            var reader = new GatePassListReader(db);
            var result = await reader.ReadAsync(2, 1, default);
            Assert.Equal(2, result.Total);
            Assert.Equal(2, Assert.Single(result.Items).Id);
            Assert.Equal("QR2", result.Items[0].QrCode);
            Assert.Empty((await reader.ReadAsync(int.MaxValue, 100, default)).Items);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
