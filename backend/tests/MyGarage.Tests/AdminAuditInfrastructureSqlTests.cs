using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Audit;
using MyGarage.Data;
using MyGarage.Infrastructure.Audit;
using Xunit;

namespace MyGarage.Tests;

public sealed class AdminAuditInfrastructureSqlTests
{
    [LocalSqlFact]
    public async Task Reader_MatchesLegacyOnSqlAndCannotWrite()
    {
        var name = "MyGarageAuditRead_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        var target = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<AuditReadDbContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new AuditReadDbContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AuditLogs (Id bigint NOT NULL PRIMARY KEY, UserId bigint NOT NULL, UserType varchar(20) NOT NULL, BranchId bigint NULL, Action varchar(50) NOT NULL, TableName varchar(100) NOT NULL, RecordId bigint NOT NULL, OldValue nvarchar(max) NULL, NewValue nvarchar(max) NULL, IpAddress varchar(50) NULL, UserAgent nvarchar(500) NULL, SessionId varchar(255) NULL, Timestamp datetime2 NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.AuditLogs (Id, UserId, UserType, Action, TableName, RecordId, NewValue, Timestamp) VALUES (1, 2, 'Staff', 'UPDATE', 'Orders', 1, N'Đã sửa', '2026-01-01'), (2, 2, 'Staff', 'UPDATE', 'Orders', 2, N'Đã sửa', '2026-01-01'), (3, 2, 'Staff', 'UPDATE', 'AuditLogs', 3, NULL, '2026-01-01')");
            var filter = new AdminAuditFilter(1, 1, "UPDATE", "ORDERS", null, null, null);
            var moved = await new AdminAuditReader(db).ReadAsync(filter, default);
            var legacyOptions = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(target.ConnectionString).Options;
            await using var legacyDb = new GarageContext(legacyOptions, new HttpContextAccessor());
            var legacy = await new MyGarage.Services.AdminAuditReader(legacyDb).ReadAsync(filter, default);
            Assert.Equal(2, moved.Total);
            Assert.Equal(legacy.Total, moved.Total);
            Assert.Equal(legacy.Items, moved.Items);
            Assert.Equal(2, Assert.Single(moved.Items).Id);
            Assert.Empty((await new AdminAuditReader(db).ReadAsync(filter with { Page = int.MaxValue }, default)).Items);
            await Assert.ThrowsAsync<NotSupportedException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
